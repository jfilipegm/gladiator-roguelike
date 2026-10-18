#!/usr/bin/env python3
"""List GC-allocating frames after warm-up in a FrameStatsRecorder CSV, plus frame/gameplay stats at >= 250 enemies.
Usage: Tools/gcscan.py Profiling/<run>.csv [warmup_seconds=3]"""
import csv,sys,statistics
rows=list(csv.DictReader(open(sys.argv[1])))
warm=float(sys.argv[2]) if len(sys.argv)>2 else 3.0
t0=float(rows[0]['time_s'])
n=0;tot=0
for i,r in enumerate(rows):
    b=int(float(r['gc_bytes'] or 0)); t=float(r['time_s'])-t0
    if b>0 and t>=warm:
        n+=1; tot+=b
        print("t=%6.2f gc=%6dB n=%3s gameplay=%.2fms enemies=%s pickups=%s ts=%s" % (t,b,r['gc_count'],float(r['gameplay_ms']),r['enemies'],r['pickups'],r['timescale']))
dur=float(rows[-1]['time_s'])-t0-warm
print("after %.0fs warm-up: alloc frames=%d total=%dB over %.0fs (%.0f B/s)" % (warm,n,tot,dur,tot/max(dur,1)))
ss=[r for r in rows if int(float(r['enemies'] or 0))>=250]
if ss:
    fm=sorted(float(r['frame_ms']) for r in ss); gm=sorted(float(r['gameplay_ms']) for r in ss)
    p=lambda a,q: a[min(len(a)-1,int(q*len(a)))]
    print("at >=250 enemies: frames=%d avg=%.2fms p95=%.2f p99=%.2f max=%.2f fps=%.0f | gameplay avg=%.3f p95=%.3f max=%.2f" % (len(ss),statistics.mean(fm),p(fm,.95),p(fm,.99),fm[-1],1000/statistics.mean(fm),statistics.mean(gm),p(gm,.95),gm[-1]))
