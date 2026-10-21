"""Extra development-only comparison of the shipped baseline and the Gemma reader on synthetic_dev_v1 (reads baseline_results.jsonl, gemma_results.jsonl, gemma_summary.json; read-only analysis, no model call). Run from the repository root with any Python 3."""
import json,collections,sys
R=[json.loads(l) for l in open('tests/synthetic_dev_v1/gemma_results.jsonl',encoding='utf-8')]
B={json.loads(l)['id']:json.loads(l) for l in open('tests/synthetic_dev_v1/baseline_results.jsonl',encoding='utf-8')}
CR={'CONFIRM_IDENTITY','STATE_ALLERGY','CONSENT_SIGN','HANDOVER_CRITICAL_ELEMENT','SBAR_ELEMENT'}
def sup(x): return [f['objective'] for f in x['raw']['findings'] if f['verdict']=='SUPPORTED']
def named(x): return [f['objective'] for f in x['raw']['findings']]
def performed_set(x):
    if x['cls']=='G': return {a.split(':')[0] for a in x['acts'] if a.endswith(':performed')}
    if x['cls'] in('A','B','D') : return {x['objective']}
    return set()
def raw_ok(x):
    if x['raw']['status']!='VALID': return False
    s=set(sup(x)); c=x['cls']
    if c in('A','B','G'): return s==performed_set(x)
    if c=='D': return x['objective'] in s
    if c in('C',): return not s
    if c=='E': return not s   # critical not supported
    if c=='F': return not s
def base_ok(x):
    b=B[x['id']]; rd=b['reading']; c=x['cls']; head=rd.get('head_act'); st=rd.get('status')
    if c in('A','B'): return head==x['objective']
    if c=='G': return head in performed_set(x)
    if c=='D': return bool(rd.get('cue') or rd.get('head_critical'))
    if c in('C','E','F'): return head=='NONE' or st not in('actual','question')
def final_ok(x,offer,cred):
    g=x['gold']
    if g['credit_eligible']: return cred and offer in g['credit_acts']
    return not offer
print('RAW SEMANTIC (scenario-independent) accuracy by class: raw | baseline reading')
for c in 'ABCDEFG':
    L=[x for x in R if x['cls']==c]
    print(c,len(L),sum(raw_ok(x) for x in L),sum(base_ok(x) for x in L))
tot=len(R); print('all',sum(raw_ok(x) for x in R),sum(base_ok(x) for x in R))
# crosstab all items: reading ok x final ok
for name,okf,offf,crf in (('Gemma',raw_ok,lambda x:x['offer'],lambda x:x['credited']),('Baseline',base_ok,lambda x:B[x['id']]['offer'],lambda x:B[x['id']]['credited'])):
    c=collections.Counter()
    for x in R:
        c[(okf(x),final_ok(x,offf(x),crf(x)))]+=1
    print(name,'reading_ok,final_ok:',dict(c))
# raw false supports on no-credit items decomposed
print('raw false single-act offers on no-credit by class:',collections.Counter(x['cls'] for x in R if not x['gold']['credit_eligible'] and len(set(sup(x))-CR)==1))
print('raw supported any noncritical on no-credit by class:',collections.Counter(x['cls'] for x in R if not x['gold']['credit_eligible'] and set(sup(x))-CR))
# by situation: raw 'not supported' correctness for non-performed ordinary (C)
for sit in('negated','intended_later','hypothetical','reported_about_someone_else','other_actor_does_it'):
    L=[x for x in R if x['cls']=='C' and x['situation']==sit]
    bs=[B[x['id']]['reading'].get('status') for x in L]
    print(sit,len(L),'gemma raw wrongly supports',sum(1 for x in L if sup(x)),'invalid',sum(1 for x in L if x['raw']['status']!='VALID'),'| shipped status misses',sum(1 for s in bs if s in('actual','question')),'| baseline false offers',sum(1 for x in L if B[x['id']]['offer']),'| gemma final offers',sum(1 for x in L if x['offer']))
# confusions among performed ordinary items (A,B): supported single vs objective
conf=collections.Counter()
for x in R:
    if x['cls'] in('A','B') and x['raw']['status']=='VALID':
        s=set(sup(x))
        if s!={x['objective']}: conf[(x['objective'],'+'.join(sorted(s)) or 'none')]+=1
print('confusions',conf.most_common())
# ESC vs VO, question vs request
for ob in('ESCALATE_SPEECH','VOICE_ORDER'):
    L=[x for x in R if x['cls'] in('A','B') and x['objective']==ob]
    print(ob,len(L),'raw ok',sum(raw_ok(x) for x in L),'baseline head ok',sum(base_ok(x) for x in L),'raw sup other',[ '+'.join(sup(x)) for x in L if set(sup(x))!={ob}])
for f in('statement','question','request_or_instruction'):
    L=[x for x in R if x['cls'] in('A','B') and x['form']==f]
    print(f,len(L),'raw ok',sum(raw_ok(x) for x in L),'baseline head ok',sum(base_ok(x) for x in L),'final credited(eligible)',sum(1 for x in L if x['credited']),'elig',sum(1 for x in L if x['gold']['credit_eligible']),'baseline credited',sum(1 for x in L if B[x['id']]['credited']))
# gemma gate stage for raw-correct performed items
print('A items raw ok:',sum(raw_ok(x) for x in R if x['cls']=='A'),'/146; final credited',sum(1 for x in R if x['cls']=='A' and x['credited']))
# inconsistent: multiple supported on single-intent items
print('single-intent items where Gemma supports 2:',sum(1 for x in R if x['cls'] in('A','B','C','D','E','F') and len(sup(x))>1))
# critical
D=[x for x in R if x['cls']=='D']; E=[x for x in R if x['cls']=='E']
print('D critical supported by gemma',sum(1 for x in D if x['objective'] in sup(x)),'/',len(D),' named',sum(1 for x in D if x['objective'] in named(x)))
print('E critical supported (wrong)',sum(1 for x in E if x['objective'] in sup(x)),'/',len(E),' named',sum(1 for x in E if x['objective'] in named(x)))
print('gemma final critical decisions',collections.Counter(x['decision'] for x in R if x['gold']['critical']))
# non critical menu diverted
print('non-critical to MENU baseline',sum(1 for x in R if B[x['id']]['decision']=='MENU' and not x['gold']['critical']),'gemma',sum(1 for x in R if x['decision']=='MENU' and not x['gold']['critical']),'eligible gemma',sum(1 for x in R if x['decision']=='MENU' and x['gold']['credit_eligible']))
print('\n==== ELIGIBLE ATTRIBUTION')
E_=[x for x in R if x['gold']['credit_eligible']]
good=lambda x:x['credited'] and x['offer'] in x['gold']['credit_acts']
tab=collections.Counter()
for x in E_:
    if good(x): tab['credited']+=1; continue
    if raw_ok(x): tab['Gemma understood; blocked by: '+x['gate_stage']]+=1
    else:
        if x['cls']=='G': tab['Gemma raw wrong on multi-intent item: '+x['raw_outcome']]+=1
        else: tab['Gemma raw wrong: '+x['raw_outcome']]+=1
for k,v in sorted(tab.items(),key=lambda kv:-kv[1]): print(v,k)
print('semantic-wrong total',sum(v for k,v in tab.items() if 'raw wrong' in k),'understood-but-blocked',sum(v for k,v in tab.items() if 'understood' in k))
print('multi-intent G with Gemma both acts supported and blocked by one-candidate rule:',sum(1 for x in R if x['cls']=='G' and raw_ok(x) and x['gate_stage']=='model supported several objectives'))
print('\n==== BASELINE ELIGIBLE ATTRIBUTION')
bt=collections.Counter()
for x in E_:
    b=B[x['id']]
    if b['credited'] and b['offer'] in x['gold']['credit_acts']: bt['credited']+=1; continue
    f=b.get('failure') or ('?','?')
    ok=base_ok(x)
    bt[('classifier understood; blocked by: ' if ok else 'classifier wrong: ')+(f[1].split(' (')[0])]+=1
for k,v in sorted(bt.items(),key=lambda kv:-kv[1]): print(v,k)
print('\n==== NO-CREDIT ITEMS: false credits')
print('baseline',sum(1 for x in R if not x['gold']['credit_eligible'] and B[x['id']]['credited']),'gemma final',sum(1 for x in R if not x['gold']['credit_eligible'] and x['credited']))
print('B items raw semantically right (right act, performed) but scenario ineligible:',sum(1 for x in R if x['cls']=='B' and raw_ok(x)),'/44 ; final credited 0')
print('C raw wrong support:',sum(1 for x in R if x['cls']=='C' and sup(x)),'/140 ; final offers',sum(1 for x in R if x['cls']=='C' and x['offer']),'; baseline',sum(1 for x in R if x['cls']=='C' and B[x['id']]['offer']))
print('F raw support',sum(1 for x in R if x['cls']=='F' and sup(x)))
print('\n==== PER OBJECTIVE recall (eligible): baseline | gemma raw(exact set) | gemma final')
objs=sorted({x['objective'] for x in R if x['gold']['credit_eligible']})
for o in objs:
    L=[x for x in E_ if x['objective']==o and x['cls']!='G']
    print(f"{o:22} n={len(L):2} base={sum(1 for x in L if B[x['id']]['credited'] and B[x['id']]['offer'] in x['gold']['credit_acts']):2} raw={sum(raw_ok(x) for x in L):2} final={sum(good(x) for x in L):2}")
print('\n==== PER SITUATION no-credit: baseline false offers | gemma raw wrong support | gemma final offers')
for sit in ('negated','intended_later','hypothetical','reported_about_someone_else','other_actor_does_it','small_talk','thinking_aloud','filler_or_noise_words','off_topic_statement','near_miss','critical_topic_discussion'):
    L=[x for x in R if x['situation']==sit]
    print(f"{sit:28} n={len(L):2} base={sum(1 for x in L if B[x['id']]['offer'])} raw={sum(1 for x in L if set(sup(x))-CR)} final={sum(1 for x in L if x['offer'])}")
print('\n==== contrast sets per kind')
S=json.load(open('tests/synthetic_dev_v1/gemma_summary.json',encoding='utf-8'))
ps=S['contrast_sets']['per_set']
print('ordinary sets all-correct: baseline',sum(1 for k,v in ps.items() if v['baseline'] and not k.startswith('SET-'+'CONFIRM') and k.split('-')[1] not in CR),'raw',sum(1 for k,v in ps.items() if v['raw'] and k.split('-')[1] not in CR),'final',sum(1 for k,v in ps.items() if v['final'] and k.split('-')[1] not in CR),'of 28')
print('critical sets (5): baseline',sum(1 for k,v in ps.items() if v['baseline'] and k.split('-')[1] in CR),'raw',sum(1 for k,v in ps.items() if v['raw'] and k.split('-')[1] in CR),'final',sum(1 for k,v in ps.items() if v['final'] and k.split('-')[1] in CR))
print('invalid replies:',[(x['id'][4:],x['situation'],x['raw']['codes']) for x in R if x['raw']['status']!='VALID'])
print('retried:',[(x['id'][4:],x['raw']['attempts'],x['raw']['status']) for x in R if x['raw']['attempts']>1][:14])
