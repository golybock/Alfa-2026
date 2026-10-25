import csv, statistics as st, sys, os
d = os.path.dirname(os.path.abspath(__file__))
def rd(n): return list(csv.DictReader(open(os.path.join(d,n),encoding='utf-8')))
def f(x): return float(x) if x not in ('',None) else None
def pct(v,p):
    v=sorted(v)
    if not v: return None
    k=(len(v)-1)*p; lo=int(k); hi=min(lo+1,len(v)-1)
    return v[lo]+(v[hi]-v[lo])*(k-lo)
u = rd('llm_usage.csv'); cards = rd('cards.csv'); news = rd('news_latency.csv'); jobs = rd('jobs.csv')
CUT='2026-10-24 14:40'
cardids = {c['card_id'] for c in cards if c['created_at']>=CUT}
print('== STAGES (card pipeline) window: all / since', CUT)
for label, flt in (('ALL', lambda r: True), ('CUR', lambda r: r['card_id'] in cardids)):
    print('--',label)
    print('stage        paid_n cache_n err_n unk_cost  in_tok  out_tok reas_tok   rub   sec  rub/req sec/req')
    for stg in ('plan','draft','critic','repair'):
        rows=[r for r in u if r['stage']==stg and flt(r)]
        paid=[r for r in rows if r['cache_hit']=='f']
        ch=len(rows)-len(paid)
        err=sum(1 for r in paid if r['error'])
        unk=sum(1 for r in paid if r['actual_cost_rub']=='')
        # abandoned: cost is reserve estimate
        ab=sum(1 for r in paid if r['error']=='request_abandoned')
        rub=sum(f(r['actual_cost_rub']) or 0 for r in paid if r['error']!='request_abandoned')
        sec=sum(int(r['duration_ms']) for r in paid)/1000
        inp=sum(int(r['input_tokens']) for r in paid); out=sum(int(r['output_tokens']) for r in paid); rs=sum(int(r['reasoning_tokens'] or 0) for r in paid)
        n=len(paid)
        print(f'{stg:12} {n:5} {ch:7} {err:5} {unk:4}+{ab}ab {inp:8} {out:8} {rs:8} {rub:7.2f} {sec:7.0f} {rub/max(n,1):6.2f} {sec/max(n,1):6.1f}')
print()
print('== CARDS')
for label,sel in (('ALL',cards),('CUR',[c for c in cards if c['card_id'] in cardids])):
    tot=len(sel); by={}
    for c in sel: by[c['status']]=by.get(c['status'],0)+1
    real=[c for c in sel if int(c['calls'])>1]  # exclude budget_exhausted 1-call
    ready=[c for c in sel if c['status']=='ready']
    print(label,'cards',tot,by,'with_llm_work',len(real))
    for key in ('plan_rub','draft_rub','critic_rub','repair_rub','total_rub'):
        v=[f(c[key]) for c in ready]
        print(f'  ready {key}: mean {st.mean(v):.2f} median {st.median(v):.2f} p90 {pct(v,.9):.2f} sum {sum(v):.2f}')
    wall=[f(c['wall_s']) for c in ready if f(c['wall_s'])<3600]
    print('  ready wall_s: n',len(wall),'p50',pct(wall,.5),'p90',pct(wall,.9),'max',max(wall))
    print('  ready with repair:',sum(1 for c in ready if int(c['repair_calls'])>0),'/',len(ready))
    # cost of all card spend incl. failed
    allcost=sum(f(c['total_rub']) for c in sel)
    failed=[c for c in sel if c['status']=='failed' and int(c['calls'])>1]
    print('  total card spend',round(allcost,2),'ready',round(sum(f(c['total_rub']) for c in ready),2),'failed w/ work',len(failed),round(sum(f(c['total_rub']) for c in failed),2))
    if ready:
        print('  rub per READY card incl. failed cost:', round(allcost/len(ready),2))
    for role in ('ceo','cfo'):
        v=[f(c['total_rub']) for c in ready if c['role']==role]
        print('  ',role,len(v),'mean',round(st.mean(v),2))
print()
print('== NEWS')
by={}
for n in news: by[(n['news_status'])]=by.get(n['news_status'],0)+1
print(by)
acc=[n for n in news if n['news_status']=='accepted' and n['event_created']]
f2e=[f(n['fetched_to_event_s']) for n in acc]
print('accepted->event n',len(acc),'p50',pct(f2e,.5),'p90',pct(f2e,.9),'max',max(f2e))
f3=[x for x in f2e if x<3600]
print('  <1h: n',len(f3),'p50',pct(f3,.5),'p90',pct(f3,.9),'max',max(f3))
run=[f(n['parse_run_s']) for n in acc if n['parse_run_s']]
wait=[f(n['parse_wait_s']) for n in acc if n['parse_wait_s']]
print('  parse_run n',len(run),'p50',pct(run,.5),'p90',pct(run,.9),'; wait p50',pct(wait,.5),'p90',pct(wait,.9),'max',max(wait))
print('== JOBS (done, run_s / wait_s)')
for k in ('CollectNews','TriageNews','ParseNews','GenerateInsight','RefreshFeed','AnswerChat'):
    j=[x for x in jobs if x['kind']==k and x['status']=='done' and x['run_s']]
    r=[f(x['run_s']) for x in j]; w=[f(x['wait_s']) for x in j if x['wait_s']]
    w1=[x for x in w if x<3600]
    print(f'{k:16} n={len(j):4} run p50={pct(r,.5):.1f} p90={pct(r,.9):.1f} | wait<1h n={len(w1)} p50={pct(w1,.5) if w1 else None} p90={pct(w1,.9) if w1 else None} | wait all max={max(w) if w else None}')
