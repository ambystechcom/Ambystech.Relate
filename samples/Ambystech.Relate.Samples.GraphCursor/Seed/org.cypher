// Demo dataset: a small "software org" graph (about 200 nodes).
// Service, Team, Person, Repo, Dependency, CVE, Incident.
//
// Idempotent: every node is MERGEd on a stable id and every relationship is MERGEd, so loading it twice
// changes nothing. The engine runs one statement per call, so statements are separated by semicolons.
//
// Planted chain for the demo goal "find which team owns the service exposed to CVE-2026-1234":
//   (CVE-2026-1234)-[:AFFECTS]->(log-parse)<-[:DEPENDS_ON]-(payments-gateway)-[:OWNED_BY]->(Payments)
//   (Person)-[:MEMBER_OF]->(Payments), (Person)-[:ON_CALL_FOR]->(payments-gateway)
//   (INC-2026-0042)-[:INVOLVED]->(payments-gateway), (INC-2026-0042)-[:CAUSED_BY]->(CVE-2026-1234)
// Decoys: CVE-2026-1243 and CVE-2026-1324 look similar but affect other dependencies.

// 1. Teams
UNWIND [
  {id: 'team-payments', name: 'Payments', area: 'billing'},
  {id: 'team-platform', name: 'Platform', area: 'infrastructure'},
  {id: 'team-identity', name: 'Identity', area: 'auth'},
  {id: 'team-search', name: 'Search', area: 'discovery'},
  {id: 'team-data', name: 'Data', area: 'analytics'},
  {id: 'team-mobile', name: 'Mobile', area: 'apps'},
  {id: 'team-security', name: 'Security', area: 'appsec'},
  {id: 'team-growth', name: 'Growth', area: 'marketing'}
] AS row
MERGE (t:Team {id: row.id})
SET t.name = row.name, t.area = row.area;

// 2. Generated services, each owned by one team
UNWIND range(0, 39) AS i
WITH i,
     ['auth', 'search', 'catalog', 'cart', 'orders', 'ledger', 'notify', 'media', 'recs', 'billing'][i % 10]
       + '-' + ['api', 'worker', 'gateway', 'sync'][toInteger(i / 10)] AS name
MERGE (s:Service {id: 'svc-' + toString(i)})
SET s.name = name, s.tier = 'tier-' + toString(1 + i % 3), s.language = ['csharp', 'go', 'rust', 'java', 'python'][i % 5]
WITH s, i
MATCH (t:Team {id: ['team-payments', 'team-platform', 'team-identity', 'team-search', 'team-data', 'team-mobile', 'team-security', 'team-growth'][i % 8]})
MERGE (s)-[:OWNED_BY]->(t);

// 3. One repository per service, maintained by the owning team
UNWIND range(0, 39) AS i
MATCH (s:Service {id: 'svc-' + toString(i)})-[:OWNED_BY]->(t:Team)
MERGE (r:Repo {id: 'repo-' + toString(i)})
SET r.name = s.name, r.url = 'https://git.example.org/' + s.name
MERGE (s)-[:BUILT_FROM]->(r)
MERGE (r)-[:MAINTAINED_BY]->(t);

// 4. Service-to-service calls
UNWIND range(0, 39) AS i
MATCH (a:Service {id: 'svc-' + toString(i)}), (b:Service {id: 'svc-' + toString((i * 7 + 3) % 40)})
WHERE a <> b
MERGE (a)-[:CALLS]->(b);

// 5. Third-party dependencies (29 ordinary ones; the vulnerable library is added separately)
WITH ['tls-kit', 'json-fast', 'http-core', 'retry-lib', 'metrics-agent', 'cache-client', 'queue-sdk', 'orm-lite',
      'crypto-utils', 'csv-reader', 'grpc-bridge', 'yaml-parse', 'oauth-client', 'pdf-render', 'image-resize',
      'geo-lookup', 'cron-runner', 'feature-flags', 'rate-limiter', 'tracing-sdk', 'uuid-gen', 'mail-client',
      'search-client', 'html-sanitize', 'proto-codec', 'zip-stream', 'date-utils', 'config-loader', 'log-format'] AS names
UNWIND range(0, size(names) - 1) AS i
MERGE (d:Dependency {id: 'dep-' + names[i]})
SET d.name = names[i], d.version = '1.' + toString(i % 9) + '.0', d.idx = i;

// 6. Each generated service depends on three ordinary dependencies
UNWIND range(0, 39) AS i
MATCH (s:Service {id: 'svc-' + toString(i)})
UNWIND [i % 29, (i * 3 + 1) % 29, (i * 7 + 2) % 29] AS k
MATCH (d:Dependency {idx: k})
MERGE (s)-[:DEPENDS_ON]->(d);

// 7. Generated CVEs, each affecting one ordinary dependency
UNWIND range(1, 20) AS i
MERGE (c:CVE {id: 'CVE-2026-' + toString(1000 + i)})
SET c.severity = ['low', 'medium', 'high', 'critical'][i % 4], c.published = '2026-0' + toString(1 + i % 9) + '-15'
WITH c, i
MATCH (d:Dependency {idx: (i * 5 + 3) % 29})
MERGE (c)-[:AFFECTS]->(d);

// 8. The planted chain: the vulnerable library, the exposed service and the CVE
MERGE (d:Dependency {id: 'dep-log-parse'})
SET d.name = 'log-parse', d.version = '3.2.1', d.idx = 99;

MERGE (s:Service {id: 'svc-payments-gateway'})
SET s.name = 'payments-gateway', s.tier = 'tier-1', s.language = 'csharp', s.exposure = 'internet'
WITH s
MATCH (t:Team {id: 'team-payments'})
MERGE (s)-[:OWNED_BY]->(t)
WITH s, t
MATCH (d:Dependency {id: 'dep-log-parse'})
MERGE (s)-[:DEPENDS_ON]->(d)
WITH s, t
MERGE (r:Repo {id: 'repo-payments-gateway'})
SET r.name = 'payments-gateway', r.url = 'https://git.example.org/payments-gateway'
MERGE (s)-[:BUILT_FROM]->(r)
MERGE (r)-[:MAINTAINED_BY]->(t);

MATCH (s:Service {id: 'svc-payments-gateway'}), (d:Dependency)
WHERE d.id IN ['dep-tls-kit', 'dep-http-core']
MERGE (s)-[:DEPENDS_ON]->(d);

MATCH (s:Service {id: 'svc-payments-gateway'}), (o:Service)
WHERE o.id IN ['svc-3', 'svc-9']
MERGE (s)-[:CALLS]->(o);

MERGE (c:CVE {id: 'CVE-2026-1234'})
SET c.severity = 'critical', c.published = '2026-03-02', c.summary = 'Remote code execution in log-parse'
WITH c
MATCH (d:Dependency {id: 'dep-log-parse'})
MERGE (c)-[:AFFECTS]->(d);

// 9. Decoy CVEs with similar ids that affect other dependencies
MERGE (c:CVE {id: 'CVE-2026-1243'})
SET c.severity = 'high', c.published = '2026-03-04', c.summary = 'Certificate validation bypass in tls-kit'
WITH c
MATCH (d:Dependency {id: 'dep-tls-kit'})
MERGE (c)-[:AFFECTS]->(d);

MERGE (c:CVE {id: 'CVE-2026-1324'})
SET c.severity = 'medium', c.published = '2026-03-09', c.summary = 'Header injection in http-core'
WITH c
MATCH (d:Dependency {id: 'dep-http-core'})
MERGE (c)-[:AFFECTS]->(d);

// 10. People: 40, five per team
UNWIND range(0, 39) AS i
WITH i,
     ['Ada', 'Grace', 'Linus', 'Margaret', 'Barbara', 'Dennis', 'Ken', 'Edsger'][i % 8] AS first,
     ['Okafor', 'Silva', 'Nakamura', 'Haddad', 'Petrov', 'Alvarez', 'Chen', 'Novak', 'Larsen', 'Mensah'][i % 10] AS last
MERGE (p:Person {id: 'person-' + toString(i)})
SET p.name = first + ' ' + last, p.role = ['engineer', 'engineer', 'engineer', 'manager'][i % 4]
WITH p, i
MATCH (t:Team {id: ['team-payments', 'team-platform', 'team-identity', 'team-search', 'team-data', 'team-mobile', 'team-security', 'team-growth'][i % 8]})
MERGE (p)-[:MEMBER_OF]->(t);

// 11. On-call rotations (each person is on call for a service their own team owns)
UNWIND range(0, 39, 5) AS i
MATCH (p:Person {id: 'person-' + toString(i)}), (s:Service {id: 'svc-' + toString(i)})
MERGE (p)-[:ON_CALL_FOR]->(s);

MATCH (p:Person {id: 'person-0'}), (s:Service {id: 'svc-payments-gateway'})
MERGE (p)-[:ON_CALL_FOR]->(s);

// 12. Incidents
UNWIND range(1, 15) AS i
MATCH (s:Service {id: 'svc-' + toString((i * 2) % 40)})
MERGE (n:Incident {id: 'INC-2026-' + toString(100 + i)})
SET n.title = 'Elevated errors in ' + s.name, n.severity = ['sev3', 'sev2', 'sev1'][i % 3]
MERGE (n)-[:INVOLVED]->(s);

UNWIND range(2, 14, 2) AS i
MATCH (n:Incident {id: 'INC-2026-' + toString(100 + i)}), (c:CVE {id: 'CVE-2026-' + toString(1000 + i)})
MERGE (n)-[:CAUSED_BY]->(c);

MERGE (n:Incident {id: 'INC-2026-0042'})
SET n.title = 'Remote code execution attempt against payments-gateway', n.severity = 'sev1'
WITH n
MATCH (s:Service {id: 'svc-payments-gateway'}), (c:CVE {id: 'CVE-2026-1234'})
MERGE (n)-[:INVOLVED]->(s)
MERGE (n)-[:CAUSED_BY]->(c);

// 13. Marker: makes loading the seed twice a no-op
MERGE (m:Meta {key: 'seed'})
SET m.version = 1;
