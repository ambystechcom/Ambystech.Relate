using System.Collections.Concurrent;
using Ambystech.Relate.Samples.GraphCursor.Audit;
using Ambystech.Relate.Samples.GraphCursor.Scoring;

namespace Ambystech.Relate.Samples.GraphCursor.Cursor;

/// <summary>
/// The cursor protocol: open, next_hop, take, gather, walk (and the optional run_query). Everything the agent
/// can name comes from a previous response, so a label, relationship type or node that is not in the store
/// can never be reached. MCP-free, so tests drive it directly.
/// </summary>
public sealed class CursorService
{
    private readonly StoreRegistry _stores;
    private readonly CursorOptions _options;
    private readonly IHopScorer _scorer;
    private readonly AuditLog _audit;
    private readonly TimeProvider _clock;
    private readonly GraphReader _reader;
    private readonly ConcurrentDictionary<string, CursorSession> _sessions = new(StringComparer.Ordinal);

    public CursorService(StoreRegistry stores, CursorOptions options, IHopScorer scorer, AuditLog audit, TimeProvider clock)
    {
        _stores = stores;
        _options = options;
        _scorer = scorer;
        _audit = audit;
        _clock = clock;
        _reader = new GraphReader(options);
    }

    public bool RawQueryEnabled => _options.AllowRawQuery;

    /// <summary>Create a cursor positioned on a start node set.</summary>
    public OpenResult Open(string? store, string? startQuery)
    {
        var (name, db) = _stores.Resolve(store);
        var start = ResolveStart(db, startQuery);
        if (start.Count == 0)
        {
            throw new CursorException("empty_start", "The start query matched no nodes.");
        }

        var session = new CursorSession(Guid.NewGuid().ToString("N"), name, db, _clock.GetUtcNow());
        var ids = start.Select(n => n.Id).ToList();
        session.Position = ids;
        session.Start.AddRange(ids);
        session.Issued.UnionWith(ids);
        _sessions[session.Id] = session;
        _audit.StartWalk(session.Id, name, session.StartedAt);

        var (outgoing, incoming) = RelCounts(db, ids);
        var vocabulary = _reader.BuildVocabulary(db, ids, start, outgoing, incoming);
        session.SeenLabels.UnionWith(vocabulary.Labels);
        return new OpenResult(session.Id, name, start, outgoing, incoming, vocabulary);
    }

    /// <summary>The typed choices available from <paramref name="nodeId"/>, ranked against <paramref name="goal"/>.</summary>
    public NextHopResult NextHop(string cursorId, string nodeId, string goal)
    {
        var session = Session(cursorId);
        if (string.IsNullOrWhiteSpace(goal))
        {
            throw new CursorException("goal_required", "Pass the goal you are walking toward; it is used to rank the choices.");
        }

        lock (session.Gate)
        {
            if (!session.Position.Contains(nodeId, StringComparer.Ordinal))
            {
                throw new CursorException("not_in_position", $"'{nodeId}' is not in the cursor's current position. Position: {string.Join(", ", session.Position)}.");
            }

            var groups = _reader.Groups(session.Database, nodeId);
            var samples = _reader
                .Nodes(session.Database, groups.SelectMany(g => g.SampleIds).Distinct(StringComparer.Ordinal).ToList())
                .ToDictionary(n => n.Id, StringComparer.Ordinal);

            var last = session.Steps.LastOrDefault();
            var ranked = groups
                .Select(g =>
                {
                    var views = g.SampleIds.Where(samples.ContainsKey).Select(id => samples[id]).ToList();
                    var values = views.SelectMany(v => v.Props.Values.OfType<string>()).ToList();
                    var score = _scorer.Score(goal, new HopCandidate(g.RelType, g.Direction, g.TargetLabels, values, g.Count));
                    return (Group: g, Views: views, Score: score, Backtrack: IsBacktrack(session, last, nodeId, g));
                })
                .OrderBy(x => x.Backtrack)
                .ThenByDescending(x => x.Score)
                .ThenBy(x => x.Group.RelType, StringComparer.Ordinal)
                .ThenBy(x => x.Group.Direction, StringComparer.Ordinal)
                .ThenBy(x => string.Join(',', x.Group.TargetLabels), StringComparer.Ordinal)
                .Take(_options.MaxChoices)
                .ToList();

            // Choices offered earlier from this same node are replaced by the fresh ones.
            foreach (var stale in session.Offered.Where(kv => kv.Value.FromId == nodeId).Select(kv => kv.Key).ToList())
            {
                session.Offered.Remove(stale);
            }

            var choices = new List<ChoiceView>();
            foreach (var (group, views, score, backtrack) in ranked)
            {
                var choiceId = $"c{++session.ChoiceCounter}";
                session.Offered[choiceId] = new OfferedChoice(choiceId, nodeId, group.RelType, group.Direction, group.TargetLabels, group.Count, group.SampleIds, score);
                session.Issued.UnionWith(group.SampleIds);
                session.SeenLabels.UnionWith(group.TargetLabels);
                choices.Add(new ChoiceView(choiceId, group.RelType, group.Direction, group.TargetLabels, group.Count, views, score, backtrack));
            }

            session.Goal = goal;
            _audit.SetGoal(session.Id, goal);
            return new NextHopResult(session.Id, nodeId, goal, choices);
        }
    }

    /// <summary>
    /// Move the cursor. <paramref name="target"/> is a choice id (moves to the choice's whole target set) or a node
    /// id the cursor has returned and that an offered choice leads to. Anything else is rejected.
    /// </summary>
    public TakeResult Take(string cursorId, string target)
    {
        var session = Session(cursorId);
        lock (session.Gate)
        {
            OfferedChoice choice;
            List<string> destination;

            if (session.Offered.TryGetValue(target, out var offered))
            {
                choice = offered;
                destination = _reader.Targets(session.Database, offered).ToList();
                if (destination.Count == 0)
                {
                    throw new CursorException("empty_choice", $"Choice '{target}' no longer leads anywhere.");
                }
            }
            else if (!session.Issued.Contains(target))
            {
                throw new CursorException(
                    "not_offered",
                    $"'{target}' was never returned by this cursor. Take a choiceId from next_hop or a node id it showed you.");
            }
            else
            {
                var via = session.Offered.Values
                    .Where(o => session.Position.Contains(o.FromId, StringComparer.Ordinal))
                    .OrderByDescending(o => o.Score)
                    .FirstOrDefault(o => _reader.Adjacent(session.Database, o, target));
                choice = via ?? throw new CursorException(
                    "not_reachable",
                    $"'{target}' was returned earlier but no offered choice from the current position leads to it. Call next_hop first.");
                destination = [target];
            }

            var step = new Step(++session.Seq, choice.FromId, choice.RelType, choice.Direction, destination, choice.Score, _clock.GetUtcNow());
            session.Steps.Add(step);
            session.Position = destination;
            session.Offered.Clear();
            session.Issued.UnionWith(destination);
            _audit.RecordStep(session.Id, step);

            var position = _reader.Nodes(session.Database, destination);
            var (outgoing, incoming) = RelCounts(session.Database, destination);
            session.SeenLabels.UnionWith(position.SelectMany(n => n.Labels));
            session.SeenLabels.UnionWith(outgoing.Concat(incoming).SelectMany(c => c.TargetLabels));
            return new TakeResult(session.Id, position, step, outgoing, incoming);
        }
    }

    /// <summary>A bounded local read: <c>neighborhood2</c>, <c>properties</c> or <c>label:&lt;L&gt;</c>.</summary>
    public GatherResult Gather(string cursorId, string scope)
    {
        var session = Session(cursorId);
        lock (session.Gate)
        {
            if (scope is not ("neighborhood2" or "properties") && !scope.StartsWith("label:", StringComparison.Ordinal))
            {
                throw new CursorException("bad_scope", "scope must be 'neighborhood2', 'properties' or 'label:<L>'.");
            }

            if (scope.StartsWith("label:", StringComparison.Ordinal))
            {
                var label = scope["label:".Length..];
                if (!session.SeenLabels.Contains(label))
                {
                    throw new CursorException(
                        "unknown_label",
                        $"'{label}' is not a label this cursor has seen. Seen: {string.Join(", ", session.SeenLabels.Order(StringComparer.Ordinal))}.");
                }
            }

            var (nodes, truncatedByCaps, bytes) = _reader.Cap(_reader.Gather(session.Database, scope, session.Position));
            session.Issued.UnionWith(nodes.Select(n => n.Id));
            session.SeenLabels.UnionWith(nodes.SelectMany(n => n.Labels));
            session.Evidence.Add(new EvidenceItem(scope, nodes, truncatedByCaps));
            return new GatherResult(session.Id, scope, nodes, truncatedByCaps, bytes);
        }
    }

    /// <summary>The path walked so far and the evidence gathered: the agent's context.</summary>
    public WalkResult Walk(string cursorId)
    {
        var session = Session(cursorId);
        lock (session.Gate)
        {
            var position = _reader.Nodes(session.Database, session.Position);
            return new WalkResult(session.Id, session.Store, session.Goal, session.Start.ToList(), session.Steps.ToList(), position, session.Evidence.ToList());
        }
    }

    /// <summary>The expert escape hatch. Only available with <c>--allow-raw-query</c>.</summary>
    public RawQueryResult RunQuery(string? store, string cypher)
    {
        if (!_options.AllowRawQuery)
        {
            throw new CursorException("raw_query_disabled", "run_query is disabled. Start the server with --allow-raw-query to enable it.");
        }

        var (_, db) = _stores.Resolve(store);
        return RawQuery.Execute(db, cypher, _options);
    }

    /// <summary>
    /// True when the hop retraces the step the cursor just took (same relationship type, opposite direction, back
    /// to the node it came from). Such options are ranked after the rest: the goal often mentions where you came from.
    /// </summary>
    private bool IsBacktrack(CursorSession session, Step? last, string nodeId, HopGroup group)
    {
        if (last is null
            || last.RelType != group.RelType
            || last.Direction == group.Direction
            || !last.To.Contains(nodeId, StringComparer.Ordinal))
        {
            return false;
        }

        var probe = new OfferedChoice("probe", nodeId, group.RelType, group.Direction, group.TargetLabels, group.Count, [], 0);
        return _reader.Adjacent(session.Database, probe, last.From);
    }

    private IReadOnlyList<NodeView> ResolveStart(RelateDatabase db, string? startQuery)
    {
        if (string.IsNullOrWhiteSpace(startQuery))
        {
            return _reader.DefaultStart(db);
        }

        if (StartQueryParser.TryParse(startQuery, out var label, out var where))
        {
            return _reader.FindStart(db, label, where);
        }

        if (!_options.AllowRawQuery)
        {
            throw new CursorException(
                "start_query_rejected",
                $"startQuery must be {StartQueryParser.Form}. Raw Cypher needs --allow-raw-query.");
        }

        return RawQuery.Nodes(RawQuery.RunReadOnly(db, startQuery, _options), _options.MaxNodes)
            .Select(n => _reader.ToView(n))
            .ToList();
    }

    private (IReadOnlyList<RelCount> Outgoing, IReadOnlyList<RelCount> Incoming) RelCounts(RelateDatabase db, IReadOnlyCollection<string> ids) =>
        (_reader.RelCounts(db, ids, "out"), _reader.RelCounts(db, ids, "in"));

    private CursorSession Session(string cursorId) =>
        _sessions.TryGetValue(cursorId, out var session)
            ? session
            : throw new CursorException("unknown_cursor", $"No cursor '{cursorId}'. Call open first.");
}
