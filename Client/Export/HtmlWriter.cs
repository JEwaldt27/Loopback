using System.Text.Json;

namespace Client.Export;

// Builds a self-contained, pan/zoom HTML connection diagram from a document.
//
// This deliberately takes the SAME JSON that SaveDiagram writes to a .lf file rather
// than reading the live diagram: the save format already carries every sheet, every
// port and every link, so there is only one serializer to keep correct. Feeding it
// the saved shape also means this doubles as an .lf converter.
//
// The output is one file with no network dependencies — the payload is inlined — so
// it can be emailed to a client or opened off a thumb drive on site.
public static class HtmlWriter
{
    public static string Build(string lfJson)
    {
        using var doc = JsonDocument.Parse(lfJson);
        var root = doc.RootElement;

        // Cable colors come from the document's own cable types, so the export matches
        // what is on screen instead of a hardcoded palette.
        var cables = new List<object>();
        if (root.TryGetProperty("cableTypes", out var ct) && ct.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in ct.EnumerateArray())
                cables.Add(new
                {
                    name = Str(c, "name"),
                    color = Str(c, "color"),
                    partNumber = Str(c, "partNumber"),
                    cableColor = Str(c, "cableColor"),
                });
        }

        var sheets = new List<object>();
        if (root.TryGetProperty("sheets", out var sh) && sh.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in sh.EnumerateArray())
                sheets.Add(BuildSheet(s));
        }

        string title = "", client = "", number = "";
        if (root.TryGetProperty("titleBlock", out var tb) && tb.ValueKind == JsonValueKind.Object)
        {
            title = Str(tb, "ProjectName");
            client = Str(tb, "Client");
            number = Str(tb, "ProjectNumber");
        }

        var modified = "";
        if (root.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
            modified = Str(meta, "modifiedAt");

        var payload = JsonSerializer.Serialize(new
        {
            sheets,
            active = root.TryGetProperty("activeSheet", out var a) && a.TryGetInt32(out var ai) ? ai : 0,
            cables,
            project = new { name = title, client, number },
            modified,
        });

        // The payload sits inside a <script> block, so any "<" must not be able to close
        // it early. Escaping every "<" is simpler than special-casing "</script>".
        payload = payload.Replace("<", "\\u003c");

        return Template.Replace("__DATA__", payload);
    }

    private static object BuildSheet(JsonElement s)
    {
        var nodes = new List<object>();
        foreach (var n in Arr(s, "nodes"))
        {
            var ports = new List<string[]>();
            foreach (var p in Arr(n, "ports"))
                ports.Add(new[] { Str(p, "name"), Str(p, "type"), Str(p, "direction") });

            var make = $"{Str(n, "manufacturer")} {Str(n, "model")}".Trim();
            nodes.Add(new
            {
                id = Str(n, "id"),
                label = Str(n, "label"),
                make,
                cat = Str(n, "category").Trim(),
                x = Num(n, "x"),
                y = Num(n, "y"),
                ports,
            });
        }

        var links = new List<object[]>();
        foreach (var l in Arr(s, "links"))
        {
            // Links still being dragged have no target; skip rather than emit a half link.
            var src = Str(l, "sourceNodeId");
            var tgt = Str(l, "targetNodeId");
            if (src.Length == 0 || tgt.Length == 0) continue;
            links.Add(new object[]
            {
                src, Int(l, "sourcePortIndex"),
                tgt, Int(l, "targetPortIndex"),
                Str(l, "label"), Str(l, "cableType"),
            });
        }

        var boxes = new List<double[]>();
        foreach (var b in Arr(s, "boxes"))
            boxes.Add(new[] { Num(b, "x"), Num(b, "y"), Num(b, "width"), Num(b, "height") });

        var texts = new List<object[]>();
        foreach (var t in Arr(s, "texts"))
            texts.Add(new object[] { Num(t, "x"), Num(t, "y"), Str(t, "text") });

        return new { name = Str(s, "name"), nodes, links, boxes, texts };
    }

    private static IEnumerable<JsonElement> Arr(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static double Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble() : 0;

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : 0;

    private const string Template = """
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Loopback connections</title>
<style>
:root{--bg:#faf9f5;--card:#fff;--fg:#1f1e1d;--mut:#6b6a65;--bd:#d6d4cc;--zone:#b9b6ab;--row:#f3f2ee}
@media (prefers-color-scheme:dark){:root{--bg:#1f1e1d;--card:#2a2927;--fg:#ecebe6;--mut:#9c9a92;--bd:#45443f;--zone:#5f5e5a;--row:#32312e}}
*{box-sizing:border-box}html,body{margin:0;height:100%;background:var(--bg);color:var(--fg);font:13px system-ui,-apple-system,"Segoe UI",sans-serif}
header{position:absolute;top:0;left:0;right:0;display:flex;flex-wrap:wrap;gap:8px;align-items:center;padding:8px 12px;background:var(--bg);border-bottom:1px solid var(--bd);z-index:2}
select,button,input{font:inherit;color:var(--fg);background:var(--card);border:1px solid var(--bd);border-radius:6px;padding:4px 10px;cursor:pointer}
input[type=search]{cursor:text;width:160px}
label{display:inline-flex;align-items:center;gap:4px;cursor:pointer}
.chip{display:inline-flex;align-items:center;gap:6px}.chip.off{opacity:.35}
#svg{position:absolute;inset:0;width:100%;height:100%;cursor:grab;touch-action:none}#svg.drag{cursor:grabbing}
.nbox{fill:var(--card);stroke:var(--bd)}.nhead{fill:var(--row)}
.t{fill:var(--fg)}.m{fill:var(--mut)}.b{font-weight:600}
.zone{fill:none;stroke:var(--zone);stroke-dasharray:10 6;stroke-width:2}
.wire{fill:none;stroke-width:1.6}.wl{font-size:10px}.wlbg{fill:var(--bg)}
.dim{opacity:.07}.hl .wire{stroke-width:3}
#info{position:absolute;right:12px;bottom:12px;max-width:440px;max-height:45%;overflow:auto;background:var(--card);border:1px solid var(--bd);border-radius:8px;padding:8px 12px;display:none;z-index:2}
#info .r{display:grid;grid-template-columns:70px 96px 1fr;gap:8px;padding:3px 0;border-top:1px solid var(--bd)}
#empty{position:absolute;inset:0;display:none;align-items:center;justify-content:center;color:var(--mut)}
</style></head><body>
<header>
<select id="sheet"></select>
<button id="fit">Fit</button>
<label><input type="checkbox" id="only"> Only connected ports</label>
<label><input type="checkbox" id="lbl" checked> Cable labels</label>
<input type="search" id="q" placeholder="Find device or cable">
<span id="legend" style="display:flex;flex-wrap:wrap;gap:6px"></span>
<span class="m" id="stamp" style="margin-left:auto;color:var(--mut)"></span>
</header>
<svg id="svg"><g id="vp"></g></svg><div id="info"></div><div id="empty">This sheet has no devices.</div>
<script>
const DATA=__DATA__;
const COLORS={};(DATA.cables||[]).forEach(c=>{if(c.name)COLORS[c.name]=c.color||"#888780"});
const CABLE={};(DATA.cables||[]).forEach(c=>{if(c.name)CABLE[c.name]=c});
const col=t=>COLORS[t]||"#888780";
const NW=220,HH=40,PR=16;
const NS="http://www.w3.org/2000/svg",svg=document.getElementById("svg"),vp=document.getElementById("vp");
const mk=(t,a,p,txt)=>{const e=document.createElementNS(NS,t);for(const k in a)e.setAttribute(k,a[k]);if(txt!=null)e.textContent=txt;p&&p.appendChild(e);return e};
const esc=s=>String(s).replace(/[&<>"]/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c]));
let S,off=new Set(),view={x:0,y:0,k:1},sel=null;
const bits=[];
if(DATA.project&&DATA.project.name)bits.push(DATA.project.name);
if(DATA.project&&DATA.project.number)bits.push("#"+DATA.project.number);
if(DATA.project&&DATA.project.client)bits.push(DATA.project.client);
if(DATA.modified)bits.push("Modified "+String(DATA.modified).slice(0,16).replace("T"," "));
document.getElementById("stamp").textContent=bits.join("  ·  ");
const sheetSel=document.getElementById("sheet");
DATA.sheets.forEach((s,i)=>sheetSel.add(new Option(s.name||("Sheet "+(i+1)),i)));
sheetSel.value=Math.min(DATA.active||0,Math.max(0,DATA.sheets.length-1));
sheetSel.onchange=()=>{sel=null;render();fit()};
["only","lbl"].forEach(id=>document.getElementById(id).onchange=()=>render());
document.getElementById("fit").onclick=fit;
function render(){
 S=DATA.sheets[+sheetSel.value];if(!S)return;vp.innerHTML="";
 document.getElementById("empty").style.display=S.nodes.length?"none":"flex";
 const only=document.getElementById("only").checked,showL=document.getElementById("lbl").checked;
 const byId={};S.nodes.forEach(n=>byId[n.id]=n);
 const used={};S.links.forEach(l=>{(used[l[0]]=used[l[0]]||new Set()).add(l[1]);(used[l[2]]=used[l[2]]||new Set()).add(l[3])});
 S.nodes.forEach(n=>{n.rows=n.ports.map((p,i)=>i).filter(i=>!only||(used[n.id]&&used[n.id].has(i)));n.h=HH+n.rows.length*PR+6;n.py={};n.rows.forEach((pi,r)=>n.py[pi]=n.y+HH+r*PR+PR/2+2)});
 const gz=mk("g",{},vp),gw=mk("g",{},vp),gn=mk("g",{},vp),gl=mk("g",{},vp);
 S.boxes.forEach(b=>mk("rect",{x:b[0],y:b[1],width:b[2],height:b[3],rx:12,class:"zone"},gz));
 S.texts.forEach(t=>mk("text",{x:t[0],y:t[1],class:"t b","font-size":24},gz,t[2]));
 const lg=document.getElementById("legend");lg.innerHTML="";const cnt={};S.links.forEach(l=>cnt[l[5]]=(cnt[l[5]]||0)+1);
 Object.keys(cnt).sort().forEach(t=>{const b=document.createElement("button");b.className="chip"+(off.has(t)?" off":"");
  const c=CABLE[t]||{},tip=[t||"(no cable type)",c.partNumber?"P/N "+c.partNumber:"",c.cableColor?c.cableColor:""].filter(Boolean).join(" · ");
  b.title=tip;b.innerHTML='<svg width="20" height="6"><line x1="0" y1="3" x2="20" y2="3" stroke="'+col(t)+'" stroke-width="3"/></svg>'+esc(t||"(none)")+" ("+cnt[t]+")";
  b.onclick=()=>{off.has(t)?off.delete(t):off.add(t);render()};lg.appendChild(b)});
 S.wires=[];
 S.links.forEach(l=>{
  const a=byId[l[0]],b=byId[l[2]];if(!a||!b||off.has(l[5]))return;
  const ya=a.py[l[1]],yb=b.py[l[3]];if(ya==null||yb==null)return;
  const acx=a.x+NW/2,bcx=b.x+NW/2;let d,mx,my;
  if(Math.abs(acx-bcx)<NW){const x1=a.x+NW,x2=b.x+NW,bx=Math.max(x1,x2)+30+Math.min(120,Math.abs(yb-ya)*0.15);d="M"+x1+" "+ya+"C"+bx+" "+ya+","+bx+" "+yb+","+x2+" "+yb;mx=bx-8;my=(ya+yb)/2}
  else{const q=acx<bcx?[a,ya,b,yb]:[b,yb,a,ya],L=q[0],yl=q[1],R=q[2],yr=q[3];const x1=L.x+NW,x2=R.x,c=Math.max(40,Math.abs(x2-x1)*0.45);d="M"+x1+" "+yl+"C"+(x1+c)+" "+yl+","+(x2-c)+" "+yr+","+x2+" "+yr;mx=(x1+x2)/2;my=(yl+yr)/2}
  const g=mk("g",{"data-a":a.id,"data-b":b.id,"data-l":l[4]||""},gw);
  mk("path",{d,class:"wire",stroke:col(l[5])},g);
  const ap=a.ports[l[1]],bp=b.ports[l[3]];
  mk("title",{},g,(l[4]||"(unlabeled)")+" · "+(l[5]||"no cable type")+"\n"+a.label+" : "+(ap?ap[0]:"?")+"  →  "+b.label+" : "+(bp?bp[0]:"?"));
  if(showL&&l[4]){const w=l[4].length*5.6+8;mk("rect",{x:mx-w/2,y:my-7,width:w,height:14,rx:3,class:"wlbg",stroke:col(l[5]),"stroke-width":.6},g);mk("text",{x:mx,y:my+3.5,"text-anchor":"middle",class:"t wl"},g,l[4])}
  [[a,ya,b],[b,yb,a]].forEach(e=>{const n=e[0],y=e[1],o=e[2];const right=Math.abs(acx-bcx)<NW||o.x+NW/2>n.x+NW/2;mk("circle",{cx:right?n.x+NW:n.x,cy:y,r:3.2,fill:col(l[5])},gl)});
  S.wires.push(g)});
 S.nodes.forEach(n=>{
  const g=mk("g",{"data-id":n.id,style:"cursor:pointer"},gn);
  mk("rect",{x:n.x,y:n.y,width:NW,height:n.h,rx:6,class:"nbox"},g);
  mk("rect",{x:n.x+.5,y:n.y+.5,width:NW-1,height:HH-4,rx:6,class:"nhead"},g);
  mk("text",{x:n.x+8,y:n.y+16,class:"t b","font-size":13},g,n.label);
  mk("text",{x:n.x+8,y:n.y+30,class:"m","font-size":10},g,(n.make+(n.cat?" · "+n.cat:"")).slice(0,40));
  n.rows.forEach(pi=>{const p=n.ports[pi],y=n.py[pi],u=used[n.id]&&used[n.id].has(pi);
   mk("text",{x:n.x+10,y:y+4,class:u?"t b":"m","font-size":11},g,p[0]);
   mk("text",{x:n.x+NW-10,y:y+4,"text-anchor":"end",class:"m","font-size":9},g,[p[1],p[2]].filter(Boolean).join(" · "))});
  mk("title",{},g,n.label+"\n"+n.make+"\n"+n.ports.length+" ports");
  g.onmouseenter=()=>hi(n.id);g.onmouseleave=()=>hi(sel);g.onclick=e=>{e.stopPropagation();sel=sel===n.id?null:n.id;hi(sel);info(sel)}});
 hi(sel);info(sel);applyView();
}
function hi(id,lbl){const nodes=vp.querySelectorAll("g[data-id]");
 if(id==null&&!lbl){S.wires.forEach(w=>w.classList.remove("dim","hl"));nodes.forEach(n=>n.classList.remove("dim"));return}
 const keep=new Set(id?[id]:[]);S.wires.forEach(w=>{const on=lbl?w.dataset.l.toLowerCase().includes(lbl):(w.dataset.a===id||w.dataset.b===id);w.classList.toggle("dim",!on);w.classList.toggle("hl",on);if(on){keep.add(w.dataset.a);keep.add(w.dataset.b)}});
 nodes.forEach(n=>n.classList.toggle("dim",!keep.has(n.dataset.id)&&!(lbl&&n.textContent.toLowerCase().includes(lbl))));}
function info(id){const el=document.getElementById("info");if(!id){el.style.display="none";return}
 const by={};S.nodes.forEach(n=>by[n.id]=n);const n=by[id];if(!n){el.style.display="none";return}
 const rows=S.links.filter(l=>l[0]===id||l[2]===id).map(l=>{const me=l[0]===id,o=by[me?l[2]:l[0]];if(!o)return"";
  const mine=n.ports[me?l[1]:l[3]],theirs=o.ports[me?l[3]:l[1]];
  return '<div class="r"><b>'+esc(l[4]||"—")+'</b><span style="color:'+col(l[5])+'">'+esc(l[5]||"—")+'</span><span>'+esc(mine?mine[0]:"?")+" → "+esc(o.label)+" : "+esc(theirs?theirs[0]:"?")+"</span></div>"});
 el.innerHTML="<b>"+esc(n.label)+'</b> <span style="color:var(--mut)">'+esc(n.make)+" · "+rows.length+" cables</span>"+rows.join("");el.style.display="block"}
document.getElementById("q").oninput=e=>{const q=e.target.value.trim().toLowerCase();if(!q){hi(sel);return}
 const n=S.nodes.find(n=>n.label.toLowerCase()===q);n?hi(n.id):hi(null,q)};
svg.onclick=()=>{sel=null;hi(null);info(null)};
function applyView(){vp.setAttribute("transform","translate("+view.x+","+view.y+") scale("+view.k+")")}
function fit(){if(!S||!S.nodes.length)return;let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;
 S.nodes.forEach(n=>{x0=Math.min(x0,n.x);y0=Math.min(y0,n.y);x1=Math.max(x1,n.x+NW);y1=Math.max(y1,n.y+n.h)});
 S.boxes.forEach(b=>{x0=Math.min(x0,b[0]);y0=Math.min(y0,b[1]-40);x1=Math.max(x1,b[0]+b[2]);y1=Math.max(y1,b[1]+b[3])});
 // Measure the header rather than assuming its height: the legend wraps to several
 // rows on a narrow window or a document with many cable types, and a fixed guess
 // there both undersizes the drawing and pushes it below the fold.
 const HD=(document.querySelector("header")||{}).offsetHeight||50;
 const W=svg.clientWidth,H=svg.clientHeight-HD,k=Math.min(W/(x1-x0+80),H/(y1-y0+80));
 view={k,x:(W-(x1-x0)*k)/2-x0*k,y:HD+(H-(y1-y0)*k)/2-y0*k};applyView()}
svg.addEventListener("wheel",e=>{e.preventDefault();const f=Math.exp(-e.deltaY*0.0015),r=svg.getBoundingClientRect(),mx=e.clientX-r.left,my=e.clientY-r.top;view.x=mx-(mx-view.x)*f;view.y=my-(my-view.y)*f;view.k*=f;applyView()},{passive:false});
let drag=null;svg.addEventListener("pointerdown",e=>{drag={x:e.clientX,y:e.clientY,vx:view.x,vy:view.y,moved:false};svg.classList.add("drag")});
window.addEventListener("pointermove",e=>{if(!drag)return;const dx=e.clientX-drag.x,dy=e.clientY-drag.y;if(Math.abs(dx)+Math.abs(dy)>3)drag.moved=true;view.x=drag.vx+dx;view.y=drag.vy+dy;applyView()});
window.addEventListener("pointerup",()=>{if(drag&&drag.moved)svg.addEventListener("click",e=>e.stopPropagation(),{capture:true,once:true});drag=null;svg.classList.remove("drag")});
render();fit();window.onresize=fit;
</script></body></html>
""";
}
