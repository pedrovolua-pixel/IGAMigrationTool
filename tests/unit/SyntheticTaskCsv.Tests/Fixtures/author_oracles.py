"""Independent literal inputs authored from frozen column/shape contract; never invokes implementation."""
import json,hashlib,csv,io,pathlib
p=pathlib.Path(__file__).parent
h=['csv_contract_version','run_id','task_id','finding_id','package_id','scoped_option_id','assignee_id','task_revision','task_status','plan_freshness','created_at_utc','planned_at_utc','current_source_digest','planned_source_digest','export_snapshot_digest','task_link','finding_link']
run='11111111-1111-4111-8111-111111111111'
f={'profileVersion':'synthetic-profile-v1','desiredOutcomeVersion':'synthetic-outcome-lockset-v1','scoringAlgorithmVersion':'pilot-health-v1','aiPolicyVersion':'synthetic-automatic-ai-policy-v1','promptVersion':'fixture-prompt-v1','modelVersion':'synthetic-fixed-provider-v1','applicationVersion':'synthetic-phase1b-app-v1','workSchemaVersion':'synthetic-run-work-v1','scriptedResultsDigest':'1'*64,'analysisFixtureDigest':'2'*64,'maturityFixtureDigest':'3'*64,'fixPackageTemplateDigest':'a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669','fixReviewContractDigest':'a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f','planningTaskContractDigest':'f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2','phase1bLocks':{'schemaVersion':'synthetic-phase1b-input-v1','outcomeContractDigest':'f7775ef74297f0b2e563c01d435bf4800fd07a71ea9ce18558cca95a3c7af5b8','priorityPolicyVersion':'synthetic-priority-policy-v1','aiContractDigest':'471fdcb92780bac8f552366c988eab7d4f7498a123e1625aacc1dd2ecaf49af5','csvContractDigest':'108537455b4f4beac92b0cb24c526d261385662f8ff64dc6af1c5c172ef7ecc7','outcomeLockDigest':'7'*64,'aiFixtureDigest':'8'*64,'aiMappingDigest':'9'*64,'aiPacketDigest':'a'*64,'fixtureEpoch':'synthetic-phase1b-fixture-epoch-v1'}}
r={'csvContractVersion':'synthetic-task-csv-v1','runId':run,'taskId':'a'*64,'findingId':'b'*64,'packageId':'c'*64,'scopedOptionId':'d'*64,'assigneeId':'synthetic-consultant','taskRevision':7,'taskStatus':'Completed','planFreshness':'NeedsReconfirmation','createdAtUtc':'2026-10-01T12:00:00.0000000Z','plannedAtUtc':'2026-10-02T13:00:00.0000000Z','currentSourceDigest':'e'*64,'plannedSourceDigest':'f'*64,'exportSnapshotDigest':'','taskLink':f'http://localhost:5183/?run={run}&view=tasks&task='+ 'a'*64,'findingLink':f'http://localhost:5183/?run={run}&view=findings&finding='+'b'*64}
e={'schemaVersion':'synthetic-task-csv-envelope-v1','scope':{'customerId':'synthetic-customer','projectId':'synthetic-project','environmentId':'synthetic-environment'},'runId':run,'versions':{'baselineId':'synthetic-phase1b-baseline-v1','profileId':'synthetic-phase1b-combined-v1','runInputDigest':'0'*64,'frozenVersions':f},'currentSourceBinding':{'sourceDigest':'e'*64,'guidanceDigest':'1'*64,'findingReviewDigest':'2'*64,'runRevision':9,'findingRevisions':[{'findingId':'b'*64,'revision':3}]},'selectedAttestations':[{'taskId':'a'*64,'artifactId':c*64,'revision':0,'eventId':None,'kind':None,'state':'Unverified','sourceDigest':None} for c in '123'],'rows':[r],'snapshotDigest':''}
def canonical(v):return json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=True).replace('&','\\u0026')
digest=hashlib.sha256(canonical(e).encode()).hexdigest();e['snapshotDigest']=digest;r['exportSnapshotDigest']=digest
(p/'golden-envelope.json').write_text(canonical(e))
# Standard-library RFC reader/writer independently proves exact literal cell interpretation.
output=io.StringIO(newline='');writer=csv.writer(output,quoting=csv.QUOTE_ALL,lineterminator='\r\n');writer.writerow(h)
header=output.getvalue().encode();assert len(header)==283 and hashlib.sha256(header).hexdigest()=='2e1c6fedadd38cb870c4d6ef25dbd44f806fdc16f6c10ed3c20293c8a9f4b603'
(p/'golden-header.csv').write_bytes(header)
ordered=[r[k] for k in ['csvContractVersion','runId','taskId','findingId','packageId','scopedOptionId','assigneeId','taskRevision','taskStatus','planFreshness','createdAtUtc','plannedAtUtc','currentSourceDigest','plannedSourceDigest','exportSnapshotDigest','taskLink','findingLink']]
writer.writerow(ordered);b=output.getvalue().encode();(p/'golden-one-row.csv').write_bytes(b)
assert list(csv.reader(io.StringIO(b.decode(),newline='')))==[h,list(map(str,ordered))]
# Leading Unicode whitespace dangerous spreadsheet prefixes must become literal apostrophe cells.
for value in ['=1+1',' +2','\u00a0-3','\u2003@SUM(A1)','"quoted", comma']:
 first=value.lstrip()[:1];literal="'"+value if first in '=+-@' else value
 quoted='"'+literal.replace('"','""')+'"';assert list(csv.reader([quoted]))[0]==[literal]
(p/'golden-digests.txt').write_text(digest+'\n'+hashlib.sha256(b).hexdigest()+'\n')
print('independent canonical snapshot',digest,'CSV bytes',len(b),'CSV SHA256',hashlib.sha256(b).hexdigest())
