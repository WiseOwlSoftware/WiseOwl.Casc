// FR-C7 reconnaissance. Drives WiseOwl.Casc.Diablo4 against the live
// install to locate + characterise the paragon UI-definition SNO format.
//
//   dotnet run --project build/SnoScan -- groups
//   dotnet run --project build/SnoScan -- find <substr>
//   dotnet run --project build/SnoScan -- dump <groupId> <id> [Meta|Payload]
//
// Optional first arg before the command: install path (default D:\Diablo IV).
using WiseOwl.Casc.Diablo4;

string install = @"D:\Diablo IV";
var argv = args.ToList();
if (argv.Count > 0 && Directory.Exists(argv[0])) { install = argv[0]; argv.RemoveAt(0); }
if (argv.Count == 0) { Console.Error.WriteLine("usage: groups | find <substr> | dump <gid> <id> [folder]"); return 2; }

using var d4 = Diablo4Storage.Open(install);
var toc = d4.CoreToc;
var cmd = argv[0].ToLowerInvariant();

switch (cmd)
{
    case "groups":
    {
        var byGroup = toc.Entries
            .GroupBy(e => (int)e.Group)
            .OrderBy(g => g.Key);
        Console.WriteLine($"{toc.Entries.Count:N0} SNOs / {toc.GroupCount} groups");
        Console.WriteLine($"{"gid",5}  {"hash",-10}  {"count",7}  sample names");
        foreach (var g in byGroup)
        {
            uint h = toc.FormatHashFor((SnoGroup)g.Key);
            var names = string.Join(", ", g.Take(3).Select(e => e.Name));
            Console.WriteLine($"{g.Key,5}  0x{h:X8}  {g.Count(),7:N0}  {names}");
        }
        return 0;
    }
    case "find":
    {
        if (argv.Count < 2) { Console.Error.WriteLine("find <substr>"); return 2; }
        var sub = argv[1];
        var hits = toc.Entries
            .Where(e => e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => (int)e.Group).ThenBy(e => e.Name)
            .ToList();
        Console.WriteLine($"{hits.Count} hits for '{sub}'");
        Console.WriteLine($"{"gid",5}  {"hash",-10}  {"id",9}  name");
        foreach (var e in hits)
            Console.WriteLine($"{(int)e.Group,5}  0x{toc.FormatHashFor(e.Group):X8}  {e.Id,9}  {e.Name}");
        return 0;
    }
    case "snoinfo":
    {
        // snoinfo <id...> — look up any SNO id across all groups in the TOC and
        // print its (group, name). Used to identify the SNO references in
        // ParagonNode tail payloads (bonus mechanic linkages).
        if (argv.Count < 2) { Console.Error.WriteLine("snoinfo <id...>"); return 2; }
        foreach (var s in argv.Skip(1))
        {
            int id = int.Parse(s);
            var hits = toc.Entries.Where(e => e.Id == id).ToList();
            if (hits.Count == 0) Console.WriteLine($"  {id} -> (not in TOC)");
            else foreach (var e in hits) Console.WriteLine($"  {id} -> group {(int)e.Group} \"{e.Name}\"");
        }
        return 0;
    }
    case "listgroup":
    {
        if (argv.Count < 2) { Console.Error.WriteLine("listgroup <gid> [substr] [max=40]"); return 2; }
        int lg = int.Parse(argv[1]); string lsub = argv.Count > 2 ? argv[2] : "";
        int lmax = argv.Count > 3 ? int.Parse(argv[3]) : 40;
        foreach (var e in toc.Entries.Where(e => (int)e.Group == lg
                     && e.Name.Contains(lsub, StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.Name).Take(lmax))
            Console.WriteLine($"  {e.Id,9}  {e.Name}");
        return 0;
    }
    case "strings":
    {
        // Printable ASCII runs >= minlen, with offsets — widget-name landmarks.
        if (argv.Count < 3) { Console.Error.WriteLine("strings <gid> <id> [minlen] [folder]"); return 2; }
        int gidS = int.Parse(argv[1]), idS = int.Parse(argv[2]);
        int minlen = argv.Count > 3 ? int.Parse(argv[3]) : 4;
        var folderS = argv.Count > 4 ? Enum.Parse<SnoFolder>(argv[4], true) : SnoFolder.Meta;
        if (!d4.TryReadSno(gidS, idS, folderS, out var bs)) { Console.WriteLine("no content"); return 1; }
        int run = 0;
        for (int i = 0; i <= bs.Length; i++)
        {
            bool p = i < bs.Length && bs[i] is >= 0x20 and < 0x7f;
            if (p) run++;
            else
            {
                if (run >= minlen)
                {
                    int st = i - run;
                    Console.WriteLine($"{st:X6}  {System.Text.Encoding.ASCII.GetString(bs, st, run)}");
                }
                run = 0;
            }
        }
        return 0;
    }
    case "scan":
    {
        // Every offset where a 32-bit value appears (LE), e.g. a texture handle.
        if (argv.Count < 4) { Console.Error.WriteLine("scan <gid> <id> <hex32> [folder]"); return 2; }
        int gidH = int.Parse(argv[1]), idH = int.Parse(argv[2]);
        uint needle = Convert.ToUInt32(argv[3], 16);
        var folderH = argv.Count > 4 ? Enum.Parse<SnoFolder>(argv[4], true) : SnoFolder.Meta;
        if (!d4.TryReadSno(gidH, idH, folderH, out var bh)) { Console.WriteLine("no content"); return 1; }
        int n = 0;
        for (int i = 0; i + 4 <= bh.Length; i++)
            if (BitConverter.ToUInt32(bh, i) == needle)
            {
                // context: 8 LE u32 before/at the hit, + float view
                var ctx = new System.Text.StringBuilder();
                for (int j = -16; j <= 16; j += 4)
                {
                    int o = i + j;
                    if (o >= 0 && o + 4 <= bh.Length)
                        ctx.Append(j == 0 ? "[" : " ").Append(BitConverter.ToUInt32(bh, o).ToString("X8")).Append(j == 0 ? "]" : "");
                }
                Console.WriteLine($"{i:X6} {ctx}");
                n++;
            }
        Console.WriteLine($"-- {n} hit(s) for 0x{needle:X8} --");
        return 0;
    }
    case "f32":
    {
        // Interpret a region as float32[] (offsets + ints alongside).
        if (argv.Count < 5) { Console.Error.WriteLine("f32 <gid> <id> <hexoff> <count> [folder]"); return 2; }
        int gidF = int.Parse(argv[1]), idF = int.Parse(argv[2]);
        int offF = Convert.ToInt32(argv[3], 16);
        int cnt = int.Parse(argv[4]);
        var folderF = argv.Count > 5 ? Enum.Parse<SnoFolder>(argv[5], true) : SnoFolder.Meta;
        if (!d4.TryReadSno(gidF, idF, folderF, out var bf)) { Console.WriteLine("no content"); return 1; }
        for (int k = 0; k < cnt && offF + k * 4 + 4 <= bf.Length; k++)
        {
            int o = offF + k * 4;
            uint u = BitConverter.ToUInt32(bf, o);
            float f = BitConverter.ToSingle(bf, o);
            string fs = (f != 0 && Math.Abs(f) is > 1e-4f and < 1e7f) ? f.ToString("0.###") : "";
            Console.WriteLine($"{o:X6}  u32=0x{u:X8} ({(int)u,11})  f32={fs}");
        }
        return 0;
    }
    case "members":
    {
        // Enumerate the whole member vocabulary by scanning for the
        // ubiquitous separator SEP and reading the triplet
        // (memberHash, SEP, payload) around every occurrence.
        if (argv.Count < 3) { Console.Error.WriteLine("members <gid> <id> [sepHex] [folder]"); return 2; }
        int gidM = int.Parse(argv[1]), idM = int.Parse(argv[2]);
        uint sep = argv.Count > 3 ? Convert.ToUInt32(argv[3], 16) : 0x1332C78Du;
        var folderM = argv.Count > 4 ? Enum.Parse<SnoFolder>(argv[4], true) : SnoFolder.Meta;
        if (!d4.TryReadSno(gidM, idM, folderM, out var bm)) { Console.WriteLine("no content"); return 1; }
        var agg = new Dictionary<uint, (int n, Dictionary<uint,int> pays)>();
        for (int i = 4; i + 8 <= bm.Length; i += 4)
        {
            if (BitConverter.ToUInt32(bm, i) != sep) continue;
            uint mh = BitConverter.ToUInt32(bm, i - 4);
            uint pay = BitConverter.ToUInt32(bm, i + 4);
            if (!agg.TryGetValue(mh, out var e)) { e = (0, new()); }
            e.n++;
            e.pays.TryGetValue(pay, out int c); e.pays[pay] = c + 1;
            agg[mh] = e;
        }
        Console.WriteLine($"distinct member hashes: {agg.Count}  (sep 0x{sep:X8})");
        Console.WriteLine($"{"member",10}  {"n",5}  payload profile (distinct payloads: top values; float if plausible)");
        foreach (var kv in agg.OrderByDescending(k => k.Value.n))
        {
            var top = kv.Value.pays.OrderByDescending(p => p.Value).Take(6).Select(p =>
            {
                float f = BitConverter.Int32BitsToSingle((int)p.Key);
                string fv = (p.Key != 0 && Math.Abs(f) is > 1e-4f and < 1e7f) ? $"={f:0.####}f" : "";
                return $"0x{p.Key:X8}{fv}×{p.Value}";
            });
            Console.WriteLine($"0x{kv.Key:X8}  {kv.Value.n,5}  {kv.Value.pays.Count} distinct: {string.Join("  ", top)}");
        }
        return 0;
    }
    case "hash":
    {
        // GbidHash (our verified case-insensitive DJB2) of candidate
        // names, to reverse member/class hashes against a wordlist.
        if (argv.Count < 2) { Console.Error.WriteLine("hash <str> [str...]"); return 2; }
        foreach (var s in argv.Skip(1))
            Console.WriteLine($"0x{Diablo4.GbidHash(s):X8}  {s}");
        return 0;
    }
    case "h":
    {
        // Multi-algorithm hash probe to identify the UI field-name hash.
        if (argv.Count < 2) { Console.Error.WriteLine("h <str> [str...]"); return 2; }
        static uint Fnv1a(string s){ uint h=2166136261u; foreach(char c in s){ h^=(byte)c; h*=16777619u; } return h; }
        static uint Fnv1aLo(string s){ return Fnv1a(s.ToLowerInvariant()); }
        static uint Djb2(string s){ uint h=5381; foreach(char c in s) h=h*33u+(byte)c; return h; }
        static uint Djb2Lo(string s){ return Djb2(s.ToLowerInvariant()); }
        static uint Sdbm(string s){ uint h=0; foreach(char c in s) h=(byte)c+(h<<6)+(h<<16)-h; return h; }
        static uint Crc32(string s){ uint[] t=new uint[256]; for(uint i=0;i<256;i++){ uint c=i; for(int k=0;k<8;k++) c=(c&1)!=0?0xEDB88320u^(c>>1):c>>1; t[i]=c;} uint h=0xFFFFFFFFu; foreach(char ch in s) h=t[(h^(byte)ch)&0xFF]^(h>>8); return ~h; }
        foreach (var s in argv.Skip(1))
        {
            var sp = System.Text.Encoding.ASCII.GetBytes(s);
            ulong l3 = WiseOwl.Casc.Internal.CascPathHash.Of(sp);
            Console.WriteLine($"{s,-22} gbid=0x{Diablo4.GbidHash(s):X8} fnv1a=0x{Fnv1a(s):X8} fnv1aLo=0x{Fnv1aLo(s):X8} djb2=0x{Djb2(s):X8} djb2Lo=0x{Djb2Lo(s):X8} sdbm=0x{Sdbm(s):X8} crc32=0x{Crc32(s):X8} l3lo=0x{(uint)l3:X8} l3hi=0x{(uint)(l3>>32):X8}");
        }
        return 0;
    }
    case "dh":
    {
        // The exact D4 serialization hashes: DJB2 core (hash*33+c), SEED 0.
        if (argv.Count < 2) { Console.Error.WriteLine("dh <str> [str...]"); return 2; }
        foreach (var s in argv.Skip(1))
        {
            uint th = D4.TypeHash(s);
            Console.WriteLine($"{s,-26} typeHash=0x{th:X8}  fieldHash=0x{th & 0x0FFFFFFF:X8}  gbidHash=0x{D4.GbidH(s):X8}");
        }
        return 0;
    }
    case "crack":
    {
        // Recover names: hash a wordlist, match observed ids.
        // crack <wordlistFile> <targetsHexCsv> [field|type]
        if (argv.Count < 3) { Console.Error.WriteLine("crack <wordlist> <hex,hex,...> [field|type|both]"); return 2; }
        var words = File.ReadLines(argv[1]);
        var targets = argv[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Convert.ToUInt32(x.Replace("0x", ""), 16)).ToHashSet();
        string mode = argv.Count > 3 ? argv[3] : "both";
        int hits = 0;
        foreach (var w in words)
        {
            var s = w.Trim();
            if (s.Length == 0) continue;
            uint th = D4.TypeHash(s);
            uint fh = th & 0x0FFFFFFFu;
            if ((mode is "type" or "both") && targets.Contains(th))
            { Console.WriteLine($"typeHash 0x{th:X8} = \"{s}\""); hits++; }
            if ((mode is "field" or "both") && targets.Contains(fh))
            { Console.WriteLine($"fieldHash 0x{fh:X8} = \"{s}\""); hits++; }
        }
        Console.WriteLine($"-- {hits} match(es) over {targets.Count} target(s) --");
        return 0;
    }
    case "widgets":
    {
        // widgets <gid> <id> [wordlist] [nameFilter]
        // Anchor on the PROVEN encodings, not the (unpinned) record
        // header: every maximal run of 12-byte schema triplets
        // (u32@k+4 == DT_BINDABLEPROPERTY) is one widget's field list;
        // the ASCII string immediately before it is its name; the
        // 56-byte 0x22 records immediately after are its instance
        // values, positionally keyed.
        if (argv.Count < 3) { Console.Error.WriteLine("widgets <gid> <id> [wordlist] [nameFilter]"); return 2; }
        int gw = int.Parse(argv[1]), iw = int.Parse(argv[2]);
        string? wl = argv.Count > 3 ? argv[3] : null;
        string? filt = argv.Count > 4 ? argv[4] : null;
        if (!d4.TryReadSno(gw, iw, SnoFolder.Meta, out var bw)) { Console.WriteLine("no content"); return 1; }
        const uint SEP = 0x1332C78D;
        var tn = new Dictionary<uint,string> {
            [SEP]="DT_BINDABLEPROPERTY",[0xA4C42E02]="DT_INT",[0xE65047AD]="DT_FLOAT",
            [0x3D4646AB]="DT_BYTE",[0x3D47BD2C]="DT_ENUM",[0xE549F591]="DT_CSTRING",
            [0x8E266332]="DT_RGBACOLOR",[0xA4C45887]="DT_SNO",[0x2B0285C0]="StringLabelHandleEx" };
        var fnm = new Dictionary<uint,string>();
        if (wl != null) foreach (var s in File.ReadLines(wl))
        { var t=s.Trim(); if(t.Length<2)continue; uint h=0; foreach(char c in t) h=(h<<5)+h+(byte)c; fnm.TryAdd(h&0x0FFFFFFFu,t); }
        string FN(uint h)=>fnm.TryGetValue(h&0x0FFFFFFFu,out var n)?n:$"f:0x{h:X7}";
        string TN(uint h)=>tn.TryGetValue(h,out var n)?n:$"t:0x{h:X8}";
        int i2=0, shown=0;
        while (i2 + 12 <= bw.Length)
        {
            if (BitConverter.ToUInt32(bw, i2+4) != SEP) { i2 += 4; continue; }
            int runStart = i2, n = 0;
            while (i2 + 12 <= bw.Length && BitConverter.ToUInt32(bw, i2+4) == SEP)
            { n++; i2 += 12; }
            // name = nearest NUL-terminated printable run anywhere
            // before runStart (the enclosing widget; names precede a
            // GROUP of schema-run objects, not each run).
            string nm = "(?)"; int nmAt = -1;
            for (int b = runStart - 1; b > Math.Max(0, runStart - 0x4000); b--)
            {
                if (bw[b] != 0) continue;                  // candidate string end
                int en = b; int st = b;
                while (st > 0 && bw[st-1] is >=0x20 and <0x7f) st--;
                if (en - st >= 4)
                {
                    var cand = System.Text.Encoding.ASCII.GetString(bw, st, en-st);
                    // identifier-ish (widget names are CamelCase/underscore)
                    if (System.Text.RegularExpressions.Regex.IsMatch(cand, "^[A-Za-z][A-Za-z0-9_]{3,}$"))
                    { nm = cand; nmAt = st; break; }
                }
                b = st;                                    // skip past this run
            }
            int s0 = nmAt >= 0 ? nmAt : runStart;
            if (filt != null && !nm.Contains(filt, StringComparison.OrdinalIgnoreCase)) continue;
            // instance records: forward from end of run, skip to first 0x22
            int p = i2; while (p+4<=bw.Length && BitConverter.ToUInt32(bw,p)!=0x22 && p<i2+0x200) p+=4;
            var vals = new List<uint>();
            while (p+0x38<=bw.Length && BitConverter.ToUInt32(bw,p)==0x22)
            { vals.Add(BitConverter.ToUInt32(bw,p+8)); p += 0x38; }
            Console.WriteLine($"@0x{s0:X} '{nm}'  fields={n}  instrec={vals.Count}");
            for (int q=0; q<n; q++)
            {
                uint f = BitConverter.ToUInt32(bw, runStart+q*12);
                uint t = BitConverter.ToUInt32(bw, runStart+q*12+8);
                string v = q<vals.Count ? $"0x{vals[q]:X8} ({(int)vals[q]})" : "-";
                Console.WriteLine($"    {FN(f),-20} {TN(t),-20} = {v}");
            }
            if (++shown > 80 && filt==null) { Console.WriteLine("… (truncated; use nameFilter)"); break; }
        }
        return 0;
    }
    case "walk":
    {
        // walk <gid> <id> <nameSubstr> [wordlist]
        // Locate a widget by inline name; print class id, the schema run
        // (fieldHash,DT_BINDABLEPROPERTY,DT_type) triplets, and the
        // following 56-byte 0x22 instance records (value@+0x08) paired
        // positionally to the schema fields.
        if (argv.Count < 4) { Console.Error.WriteLine("walk <gid> <id> <nameSubstr> [wordlist]"); return 2; }
        int gw = int.Parse(argv[1]), iw = int.Parse(argv[2]);
        string want = argv[3];
        if (!d4.TryReadSno(gw, iw, SnoFolder.Meta, out var bw)) { Console.WriteLine("no content"); return 1; }
        var tn = new Dictionary<uint,string> {
            [0x1332C78D]="DT_BINDABLEPROPERTY",[0xA4C42E02]="DT_INT",[0xE65047AD]="DT_FLOAT",
            [0x3D4646AB]="DT_BYTE",[0x3D47BD2C]="DT_ENUM",[0xE549F591]="DT_CSTRING",
            [0x8E266332]="DT_RGBACOLOR",[0xA4C45887]="DT_SNO",[0x2B0285C0]="StringLabelHandleEx" };
        var fn = new Dictionary<uint,string>();
        if (argv.Count > 4)
            foreach (var s in File.ReadLines(argv[4]))
            { var t=s.Trim(); if(t.Length<2)continue; uint h=0; foreach(char c in t) h=(h<<5)+h+(byte)c; fn.TryAdd(h&0x0FFFFFFFu,t); }
        string FN(uint h)=>fn.TryGetValue(h&0x0FFFFFFFu,out var n)?n:$"0x{h:X8}";
        string TN(uint h)=>tn.TryGetValue(h,out var n)?n:$"0x{h:X8}";
        // find the name
        int at=-1;
        for (int i=0;i+want.Length<bw.Length;i++)
        { bool m=true; for(int j=0;j<want.Length;j++) if(bw[i+j]!=(byte)want[j]){m=false;break;} if(m){at=i;break;} }
        if (at<0){ Console.WriteLine($"name '{want}' not found"); return 1; }
        int nend=at; while(nend<bw.Length && bw[nend]!=0) nend++;
        string nm=System.Text.Encoding.ASCII.GetString(bw,at,nend-at);
        uint cls = at+0x2C<=bw.Length ? BitConverter.ToUInt32(bw,at+0x28) : 0;
        Console.WriteLine($"widget '{nm}' @0x{at:X} classId=0x{cls:X8} ({TN(cls)})");
        // schema run: nearest (x,1332C78D,y) triplets at/after name+0x28
        int k=at;
        while (k+12<=bw.Length && BitConverter.ToUInt32(bw,k+4)!=0x1332C78D && k<at+0x400) k+=4;
        var fields=new List<(uint f,uint t)>();
        while (k+12<=bw.Length && BitConverter.ToUInt32(bw,k+4)==0x1332C78D)
        { fields.Add((BitConverter.ToUInt32(bw,k),BitConverter.ToUInt32(bw,k+8))); k+=12; }
        Console.WriteLine($"schema: {fields.Count} fields @0x{(fields.Count>0? k-fields.Count*12:0):X}");
        // instance records: scan forward from k for 56-byte 0x22 records
        while (k+4<=bw.Length && BitConverter.ToUInt32(bw,k)!=0x22) k+=4;
        var vals=new List<uint>();
        int p=k;
        while (p+0x38<=bw.Length && BitConverter.ToUInt32(bw,p)==0x22)
        { vals.Add(BitConverter.ToUInt32(bw,p+8)); p+=0x38; }
        Console.WriteLine($"instance: {vals.Count} records @0x{k:X} (stride 0x38, value@+0x08)");
        for (int q=0;q<fields.Count;q++)
        {
            var (f,t)=fields[q];
            string v = q<vals.Count ? $"0x{vals[q]:X8} ({(int)vals[q]})" : "(no record)";
            Console.WriteLine($"  {FN(f),-22} : {TN(t),-20} = {v}");
        }
        return 0;
    }
    case "dump":
    {
        if (argv.Count < 3) { Console.Error.WriteLine("dump <gid> <id> [folder]"); return 2; }
        int gid = int.Parse(argv[1]);
        int id = int.Parse(argv[2]);
        var folder = argv.Count > 3
            ? Enum.Parse<SnoFolder>(argv[3], ignoreCase: true)
            : SnoFolder.Meta;
        if (!d4.TryReadSno(gid, id, folder, out var bytes))
        {
            Console.WriteLine($"no {folder} content for gid={gid} id={id}");
            return 1;
        }
        var name = toc.TryGetName((SnoGroup)gid, id, out var n) ? n : "<unknown>";
        Console.WriteLine($"gid={gid} id={id} folder={folder} name={name}");
        Console.WriteLine($"length={bytes.Length} bytes  hash=0x{toc.FormatHashFor((SnoGroup)gid):X8}");
        if (bytes.Length >= 4)
            Console.WriteLine($"u32@0x00=0x{BitConverter.ToUInt32(bytes, 0):X8}");
        if (bytes.Length >= 0x14)
            Console.WriteLine($"u32@0x10=0x{BitConverter.ToUInt32(bytes, 0x10):X8} (={BitConverter.ToInt32(bytes, 0x10)})");
        int show = Math.Min(bytes.Length, argv.Count > 4 ? int.Parse(argv[4]) : 512);
        for (int off = 0; off < show; off += 16)
        {
            var hex = new System.Text.StringBuilder();
            var asc = new System.Text.StringBuilder();
            for (int j = 0; j < 16 && off + j < show; j++)
            {
                byte b = bytes[off + j];
                hex.Append(b.ToString("X2")).Append(j == 7 ? "  " : " ");
                asc.Append(b is >= 0x20 and < 0x7f ? (char)b : '.');
            }
            Console.WriteLine($"{off:X4}  {hex,-49} {asc}");
        }
        return 0;
    }
    case "stl":
    {
        // Dump a StringList table (group 42 SNO) for a locale: every
        // label -> localized text pair.  FR-D1 recon.
        if (argv.Count < 2) { Console.Error.WriteLine("stl <sno> [locale]"); return 2; }
        int stlSno = int.Parse(argv[1]);
        var loc = argv.Count > 2 ? argv[2] : "enUS";
        var cat = d4.GetStrings(loc);
        var tbl = cat.Table(stlSno);
        if (tbl is null) { Console.WriteLine($"no table sno={stlSno} in {loc}"); return 1; }
        Console.WriteLine($"sno={stlSno} name={tbl.Name ?? "<?>"} locale={loc} entries={tbl.Entries.Count}");
        foreach (var kv in tbl.Entries)
            Console.WriteLine($"  [{kv.Key}] = {kv.Value}");
        return 0;
    }
    case "stlfind":
    {
        // Find StringList entries whose label OR value contains a
        // substring (case-insensitive), across every table. Recon only.
        if (argv.Count < 2) { Console.Error.WriteLine("stlfind <substr> [locale] [max]"); return 2; }
        var needle = argv[1];
        var loc = argv.Count > 2 ? argv[2] : "enUS";
        int max = argv.Count > 3 ? int.Parse(argv[3]) : 60;
        var cat = d4.GetStrings(loc);
        int shown = 0;
        foreach (var t in cat.Tables.Values)
        {
            foreach (var kv in t.Entries)
            {
                if (kv.Key.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                    kv.Value.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"sno={t.Sno} name={t.Name ?? "<?>"} [{kv.Key}] = {kv.Value}");
                    if (++shown >= max) { Console.WriteLine("-- truncated --"); return 0; }
                }
            }
        }
        Console.WriteLine($"-- {shown} hit(s) --");
        return 0;
    }
    case "glyphclass":
    {
        // FR-D3 recon: for every group-111 glyph, print snoId/name and
        // the int32 values of the candidate class-eligibility fixed array
        // at absolute 0x34 (payload+0x24), 12 slots. Recon only.
        int slots = argv.Count > 1 ? int.Parse(argv[1]) : 12;
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 111) continue;
            if (!d4.TryReadSno(111, e.Id, SnoFolder.Meta, out var b)) continue;
            var sb = new System.Text.StringBuilder();
            for (int s = 0; s < slots; s++)
            {
                int o = 0x34 + s * 4;
                sb.Append(o + 4 <= b.Length ? BitConverter.ToInt32(b, o) : 0);
                sb.Append(s == slots - 1 ? "" : ",");
            }
            Console.WriteLine($"{e.Id,9} len={b.Length,4} [{sb}] {e.Name}");
        }
        return 0;
    }
    case "childpaths":
    {
        // FR-14 recon: every TVFS path under Base\Child\ (uncapped).
        int max = argv.Count > 1 ? int.Parse(argv[1]) : 40;
        var paths = d4.Casc.DiagnosticPaths(
            p => p.Replace('/', '\\').IndexOf("\\child\\",
                StringComparison.OrdinalIgnoreCase) >= 0);
        Console.WriteLine($"{paths.Count} child path(s)");
        for (int i = 0; i < paths.Count && i < max; i++)
            Console.WriteLine(paths[i]);
        return 0;
    }
    case "rendercover":
    {
        // FR-C9 recon: for a UI-scene SNO, the set of every 4-aligned
        // u32 in the raw blob that resolves to a real atlas frame
        // (structural "texture-binding") vs the set ReadUiScene
        // surfaces (Fields values + ExtraLayerValues). Delta = dropped
        // bindings (a still-unmodelled shape). Recon only.
        int sc = argv.Count > 1 ? int.Parse(argv[1]) : 657304;
        if (!d4.TryReadSno(46, sc, SnoFolder.Meta, out var sb)) { Console.WriteLine("no scene"); return 1; }
        var rawH = new SortedSet<uint>();
        for (int p = 0; p + 4 <= sb.Length; p += 4)
        {
            uint v = BitConverter.ToUInt32(sb, p);
            if (v is not 0 and not 0xFFFFFFFF && d4.TryGetIconFrame(v, out _, out _))
                rawH.Add(v);
        }
        var seen = new SortedSet<uint>();
        foreach (var w in d4.ReadUiScene(sc).Widgets)
        {
            foreach (var f in w.Fields) if (f.HasValue) seen.Add(f.RawValue);
            foreach (var e in w.ExtraLayerValues) seen.Add(e);
        }
        var dropped = new SortedSet<uint>(rawH);
        dropped.ExceptWith(seen);
        Console.WriteLine($"scene {sc}: atlas-resolvable-in-raw={rawH.Count} surfaced-by-ReadUiScene={seen.Count} DROPPED={dropped.Count}");
        foreach (var h in dropped) Console.WriteLine($"  DROPPED 0x{h:X8}");
        return 0;
    }
    case "noderecon":
    {
        // FR-C16 R8 / FR-C18 recon: dump the node-run widgets of a UI scene
        // both as ReadUiScene parses them (fields + ExtraLayerValues) and as
        // a raw header/value-record map for the Template_Node_* parents — so
        // we can see WHERE each rect value record sits relative to the first
        // nested anonymous child (the parent field-scan boundary).
        int sc = argv.Count > 1 ? int.Parse(argv[1]) : 657304;
        if (!d4.TryReadSno(46, sc, SnoFolder.Meta, out var b)) { Console.WriteLine("no scene"); return 1; }

        const uint SEP = 0x1332C78D, SENT = 0xFFFFFFFF;
        static int Align8(int n) => (n + 7) & ~7;
        uint U(int o) => BitConverter.ToUInt32(b, o);
        static bool NB(byte c) => c is (>=(byte)'A' and <=(byte)'Z') or (>=(byte)'a' and <=(byte)'z') or (>=(byte)'0' and <=(byte)'9') or (byte)'_';
        static bool NS(byte c) => c is (>=(byte)'A' and <=(byte)'Z') or (>=(byte)'a' and <=(byte)'z');

        // Pass-1 header scan (mirrors UiScene.Parse).
        var starts = new List<(int at, string nm, uint cls)>();
        for (int i = 0; i < b.Length; )
        {
            if (!NS(b[i])) { i++; continue; }
            int s = i, e = i; while (e < b.Length && NB(b[e])) e++;
            int len = e - s;
            if (len >= 4 && e < b.Length && b[e] == 0)
            {
                int co = s + Align8(len + 1) + 0x10;
                if (co + 12 <= b.Length && U(co + 8) == SENT)
                { starts.Add((s, System.Text.Encoding.ASCII.GetString(b, s, len), U(co))); i = e; continue; }
            }
            i = e > i ? e : i + 1;
        }
        var clsIds = new HashSet<uint>(); foreach (var st in starts) clsIds.Add(st.cls);

        // Part A — parsed node-run view.
        var scene = d4.ReadUiScene(sc);
        var ws = scene.Widgets;
        int first = -1, last = -1;
        for (int k = 0; k < ws.Count; k++)
        {
            var nm = ws[k].Name ?? "";
            if ((nm.StartsWith("Common_Node") || nm.StartsWith("Node_")) && first < 0) first = k;
            if (nm.StartsWith("Template_Node_")) last = k;
        }
        Console.WriteLine($"== scene {sc}: node run z=[{first}..{last}] of {ws.Count} widgets ==");
        for (int k = first; k <= last && k >= 0; k++)
        {
            var w = ws[k];
            Console.WriteLine($"\nz={k} '{w.Name}' class={Diablo4.FormatTypeHash(w.ClassId)}");
            foreach (var f in w.Fields)
                Console.WriteLine($"    {Diablo4.FormatFieldHash(f.FieldHash),-26} {Diablo4.FormatTypeHash(f.TypeHash),-22} = {(f.HasValue ? $"0x{f.RawValue:X8} ({(int)f.RawValue})" : "(unbound)")}");
            if (w.ExtraLayerValues.Count > 0)
                Console.WriteLine($"    extraLayers[{w.ExtraLayerValues.Count}]: {string.Join(" ", w.ExtraLayerValues.Select(v => $"0x{v:X8}"))}");
        }

        // Part B — raw value-record map for the Template_Node_* parents.
        Console.WriteLine("\n== RAW value-record map (Template_Node_* parents) ==");
        for (int si = 0; si < starts.Count; si++)
        {
            var snm = starts[si].nm;
            bool nodeRun = snm.StartsWith("Common_Node", StringComparison.Ordinal)
                        || snm.StartsWith("Node_", StringComparison.Ordinal)
                        || snm.StartsWith("Template_Node_", StringComparison.Ordinal)
                        || snm.StartsWith("Arrow_", StringComparison.Ordinal)
                        || snm.StartsWith("Connector_", StringComparison.Ordinal)
                        || snm.StartsWith("Usage_Slot", StringComparison.Ordinal)
                        || snm.StartsWith("GlyphNodeGlow", StringComparison.Ordinal)
                        || snm.StartsWith("Rarity_Display", StringComparison.Ordinal)
                        || snm.StartsWith("Purchased_Rarity", StringComparison.Ordinal);
            if (!nodeRun) continue;
            int from = starts[si].at;
            int to = si + 1 < starts.Count ? starts[si + 1].at : b.Length;
            int ownCo = from + Align8(starts[si].nm.Length + 1) + 0x10;
            int firstChild = to;
            for (int o = ownCo + 4; o + 12 <= to; o += 4)
                if (U(o + 8) == SENT && clsIds.Contains(U(o))) { firstChild = o; break; }
            Console.WriteLine($"\n'{starts[si].nm}' @0x{from:X} class={Diablo4.FormatTypeHash(starts[si].cls)} span=[0x{from:X}..0x{to:X}) ownClassOff=0x{ownCo:X} firstChild={(firstChild < to ? $"0x{firstChild:X}" : "(none)")}");
            // child sub-record markers: every (classId, _, SENT@+8) in span
            var childAt = new List<int>();
            for (int o = ownCo + 4; o + 12 <= to; o += 4)
                if (U(o + 8) == SENT && clsIds.Contains(U(o))) childAt.Add(o);
            Console.WriteLine($"  child markers ({childAt.Count}): {string.Join(" ", childAt.Select(o => $"0x{o:X}={Diablo4.FormatTypeHash(U(o))}"))}");

            // Per sub-record (parent = [from,firstChild); child_i = [childAt[i], childAt[i+1]))
            // print its schema fields + value records paired positionally.
            void DumpRange(string tag, int lo, int hi)
            {
                var sf = new List<uint>();
                for (int kk = lo; kk + 12 <= hi; )
                { if (U(kk + 4) == SEP) { sf.Add(U(kk)); kk += 12; } else kk += 4; }
                var vr = new List<uint>();
                for (int p = lo; p + 12 <= hi; )
                {
                    if (b[p] == 0x22 && U(p) == 0x22) { vr.Add(U(p + 8)); p += 0x38; }
                    else if (U(p) == 2u && U(p + 4) == 0u) { vr.Add(U(p + 8)); p += 12; }
                    else p += 4;
                }
                Console.Write($"    {tag} [0x{lo:X}..0x{hi:X}) fields={sf.Count} values={vr.Count}: ");
                for (int q = 0; q < Math.Max(sf.Count, vr.Count); q++)
                {
                    string fn = q < sf.Count ? Diablo4.FormatFieldHash(sf[q]) : "?";
                    string vv = q < vr.Count ? $"0x{vr[q]:X8}({(int)vr[q]})" : "-";
                    Console.Write($"{fn}={vv}  ");
                }
                Console.WriteLine();
            }
            DumpRange("PARENT", from, firstChild < to ? firstChild : to);
            for (int c = 0; c < childAt.Count; c++)
            {
                int clo = childAt[c];
                int chi = c + 1 < childAt.Count ? childAt[c + 1] : to;
                DumpRange($"child{c}", clo, chi);
            }
        }
        return 0;
    }
    case "recdump":
    {
        // FR-C16 R10 recon: dump the FULL bytes of every DT_BINDABLEPROPERTY
        // value record (all 56 bytes of a 0x22 record / 12 of a tag-2 block)
        // for a named widget — to see whether a bindable property carries a
        // binding-source / condition expression beyond the literal value at
        // +0x08, or whether the tail bytes are inert (⇒ engine-code-side
        // activation). recdump <gid> <id> <nameSubstr>
        if (argv.Count < 4) { Console.Error.WriteLine("recdump <gid> <id> <nameSubstr>"); return 2; }
        int gr = int.Parse(argv[1]), ir = int.Parse(argv[2]);
        string want = argv[3];
        if (!d4.TryReadSno(gr, ir, SnoFolder.Meta, out var b)) { Console.WriteLine("no content"); return 1; }
        const uint SEP = 0x1332C78D, SENT = 0xFFFFFFFF;
        static int Align8(int n) => (n + 7) & ~7;
        uint U(int o) => BitConverter.ToUInt32(b, o);
        static bool NB(byte c) => c is (>=(byte)'A' and <=(byte)'Z') or (>=(byte)'a' and <=(byte)'z') or (>=(byte)'0' and <=(byte)'9') or (byte)'_';
        static bool NS(byte c) => c is (>=(byte)'A' and <=(byte)'Z') or (>=(byte)'a' and <=(byte)'z');

        var starts = new List<(int at, string nm)>();
        for (int i = 0; i < b.Length; )
        {
            if (!NS(b[i])) { i++; continue; }
            int s = i, e = i; while (e < b.Length && NB(b[e])) e++;
            int len = e - s;
            if (len >= 4 && e < b.Length && b[e] == 0)
            {
                int co = s + Align8(len + 1) + 0x10;
                if (co + 12 <= b.Length && U(co + 8) == SENT) { starts.Add((s, System.Text.Encoding.ASCII.GetString(b, s, len))); i = e; continue; }
            }
            i = e > i ? e : i + 1;
        }
        for (int si = 0; si < starts.Count; si++)
        {
            if (!starts[si].nm.Contains(want, StringComparison.OrdinalIgnoreCase)) continue;
            int from = starts[si].at;
            int to = si + 1 < starts.Count ? starts[si + 1].at : b.Length;
            Console.WriteLine($"\n=== '{starts[si].nm}' @0x{from:X}..0x{to:X} ===");
            // schema triplets in [from,to)
            var fields = new List<uint>();
            for (int k = from; k + 12 <= to; )
            { if (U(k + 4) == SEP) { fields.Add(U(k)); k += 12; } else k += 4; }
            // walk value records, pair to schema positionally
            int fi = 0;
            for (int p = from; p + 12 <= to; )
            {
                if (b[p] == 0x22 && U(p) == 0x22)
                {
                    string fn = fi < fields.Count ? Diablo4.FormatFieldHash(fields[fi]) : "?";
                    Console.Write($"  0x{p:X} 0x22 [{fn}]:");
                    for (int w = 0; w < 14 && p + w * 4 + 4 <= b.Length; w++)
                        Console.Write($" {(w == 2 ? "<" : "")}{U(p + w * 4):X8}{(w == 2 ? ">" : "")}");
                    Console.WriteLine();
                    p += 0x38; fi++;
                }
                else if (U(p) == 2u && U(p + 4) == 0u)
                {
                    string fn = fi < fields.Count ? Diablo4.FormatFieldHash(fields[fi]) : "?";
                    Console.WriteLine($"  0x{p:X} tag2 [{fn}]: {U(p):X8} {U(p + 4):X8} <{U(p + 8):X8}>");
                    p += 12; fi++;
                }
                else p += 4;
            }
        }
        return 0;
    }
    case "snorefs":
    {
        // FR-C16 R10: every DT_SNO / handle reference the scene makes, with
        // the referenced SNO's group+name resolved — to find any state /
        // condition / binding / controller SNO the widgets point at.
        int sc = argv.Count > 1 ? int.Parse(argv[1]) : 657304;
        var scene = d4.ReadUiScene(sc);
        const uint DT_SNO = 0xA4C45887;
        var snoFields = new Dictionary<uint, SortedSet<int>>();
        foreach (var w in scene.Widgets)
            foreach (var f in w.Fields)
                if (f.TypeHash == DT_SNO && f.HasValue && f.RawValue != 0)
                {
                    if (!snoFields.TryGetValue(f.FieldHash, out var set)) { set = new(); snoFields[f.FieldHash] = set; }
                    set.Add((int)f.RawValue);
                }
        Console.WriteLine($"scene {sc}: distinct DT_SNO fields = {snoFields.Count}");
        foreach (var kv in snoFields)
        {
            Console.WriteLine($"\nfield {Diablo4.FormatFieldHash(kv.Key)} -> {kv.Value.Count} distinct SNO refs:");
            foreach (var id in kv.Value)
            {
                string desc = "?";
                foreach (var g in toc.Entries)
                    if (g.Id == id) { desc = $"group {(int)g.Group} '{g.Name}'"; break; }
                Console.WriteLine($"    {id,9}  {desc}");
            }
        }
        // also: which widgets carry per-state image fields (hImageFramePressed etc.)
        uint[] stateImg = { 0x0D75128Cu, 0x0B63D29Bu, 0x0DAEFCAAu, 0x02330CBFu };
        string[] stateNm = { "hImageFramePressed", "hImageFrameMouseOver", "hImageFrameDisable", "hImageFrameIcon" };
        Console.WriteLine("\n=== per-state image-field bindings (non-zero) ===");
        foreach (var w in scene.Widgets)
            foreach (var f in w.Fields)
                for (int s = 0; s < stateImg.Length; s++)
                    if (f.FieldHash == stateImg[s] && f.HasValue && f.RawValue != 0)
                        Console.WriteLine($"    '{w.Name}'.{stateNm[s]} = 0x{f.RawValue:X8}");
        return 0;
    }
    case "checkfields":
    {
        // Sanity check: every KnownFieldNames / KnownTypeNames entry's symbol
        // must hash (via the canonical Diablo4 hasher) back to its key. A
        // mismatch = a mislabelled hash (the name is wrong, or imported from a
        // registry using a different checksum) — e.g. 0x093CBAA8 "eGroupType".
        int bad = 0, ok = 0;
        Console.WriteLine("=== KnownFieldNames (FieldHash) ===");
        foreach (var kv in Diablo4.KnownFieldNames.OrderBy(k => k.Key))
        {
            uint c = Diablo4.FieldHash(kv.Value);
            if (c != kv.Key) { Console.WriteLine($"  MISMATCH 0x{kv.Key:X8} = \"{kv.Value}\" but FieldHash = 0x{c:X8}"); bad++; }
            else ok++;
        }
        Console.WriteLine("=== KnownTypeNames (TypeHash) ===");
        foreach (var kv in Diablo4.KnownTypeNames.OrderBy(k => k.Key))
        {
            uint c = Diablo4.TypeHash(kv.Value);
            if (c != kv.Key) { Console.WriteLine($"  MISMATCH 0x{kv.Key:X8} = \"{kv.Value}\" but TypeHash = 0x{c:X8}"); bad++; }
            else ok++;
        }
        Console.WriteLine($"-- {ok} verified, {bad} MISMATCH(es) --");
        return 0;
    }
    case "grepgroup":
    {
        // Scan every SNO in a group's Meta for an ASCII substring; report
        // hits. grepgroup <gid> <substr> [max]
        if (argv.Count < 3) { Console.Error.WriteLine("grepgroup <gid> <substr> [max]"); return 2; }
        int gg = int.Parse(argv[1]);
        var needle = System.Text.Encoding.ASCII.GetBytes(argv[2]);
        int max = argv.Count > 3 ? int.Parse(argv[3]) : 60;
        int hits = 0, scanned = 0;
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != gg) continue;
            scanned++;
            if (!d4.TryReadSno(gg, e.Id, SnoFolder.Meta, out var b)) continue;
            for (int i = 0; i + needle.Length <= b.Length; i++)
            {
                bool m = true;
                for (int j = 0; j < needle.Length; j++) if (b[i + j] != needle[j]) { m = false; break; }
                if (m) { Console.WriteLine($"  {e.Id,9}  {e.Name}  @0x{i:X}"); hits++; break; }
            }
            if (hits >= max) { Console.WriteLine("-- truncated --"); break; }
        }
        Console.WriteLine($"-- {hits} hit(s) over {scanned} group-{gg} SNOs --");
        return 0;
    }
    case "tiledstyle":
    {
        // Decode a TiledStyle (group 103) and print its source image handle.
        // tiledstyle <id>
        if (argv.Count < 2) { Console.Error.WriteLine("tiledstyle <id>"); return 2; }
        int tid = int.Parse(argv[1]);
        if (d4.TryReadTiledStyle(tid, out var ts))
            Console.WriteLine($"TiledStyle {tid}: SourceImageHandle=0x{ts.SourceImageHandle:X8} partial={ts.HasPartialDecode}");
        else
            Console.WriteLine($"TiledStyle {tid}: could not decode");
        // also raw-scan for any UIImageHandleReference-typed values
        if (d4.TryReadSno(103, tid, SnoFolder.Meta, out var b))
        {
            Console.WriteLine("raw resolvable texture handles in the SNO:");
            var seen = new HashSet<uint>();
            for (int i = 0; i + 4 <= b.Length; i += 4)
            {
                uint v = BitConverter.ToUInt32(b, i);
                if (v is not 0 and not 0xFFFFFFFF && seen.Add(v) && d4.TryGetIconFrame(v, out int sn, out _))
                    Console.WriteLine($"  0x{v:X8} (atlas {sn})");
            }
        }
        return 0;
    }
    case "allfields":
    {
        // allfields <sceneSno> <nameSubstr> — print EVERY bound field of each
        // matching widget (and its children): field name+hash, DT type, raw
        // value, and float interpretation. RE-all-fields discipline.
        if (argv.Count < 3) { Console.Error.WriteLine("allfields <sceneSno> <nameSubstr>"); return 2; }
        int sc = int.Parse(argv[1]); string sub = argv[2];
        var tnames = new Dictionary<uint,string> {
            [0xA4C42E02]="DT_INT",[0xE65047AD]="DT_FLOAT",[0x3D4646AB]="DT_BYTE",
            [0x3D47BD2C]="DT_ENUM",[0xE549F591]="DT_CSTRING",[0x8E266332]="DT_RGBACOLOR",
            [0xA4C45887]="DT_SNO",[0x2B0285C0]="StringLabelHandleEx",[0x06C7C0E9]="DT_BINDABLEPROPERTY" };
        string TN(uint t) => tnames.TryGetValue(t, out var n) ? n : $"t:0x{t:X8}";
        void Dump(string label, IReadOnlyList<UiField> fs)
        {
            foreach (var f in fs)
            {
                if (!f.HasValue) continue;
                float fv = BitConverter.Int32BitsToSingle((int)f.RawValue);
                string ff = (f.TypeHash == 0xE65047AD && f.RawValue != 0) ? $"  f32={fv:0.####}" : "";
                Console.WriteLine($"    {label}{Diablo4.FormatFieldHash(f.FieldHash),-22} {TN(f.TypeHash),-14} = 0x{f.RawValue:X8} ({(int)f.RawValue}){ff}");
            }
        }
        foreach (var w in d4.ReadUiScene(sc).Widgets)
        {
            if (!(w.Name ?? "").Contains(sub, StringComparison.OrdinalIgnoreCase)) continue;
            Console.WriteLine($"'{w.Name}' class=0x{w.ClassId:X8} fields={w.Fields.Count(f=>f.HasValue)} children={w.Children.Count}");
            Dump("", w.Fields);
            for (int c = 0; c < w.Children.Count; c++) { Console.WriteLine($"  child[{c}] class=0x{w.Children[c].ClassId:X8}"); Dump("  ", w.Children[c].Fields); }
        }
        return 0;
    }
    case "attrmap":
    {
        // attrmap — scan ALL ParagonNode (group 106) defs, aggregate
        // AttributeId -> the node-name stat token(s) (first-party eAttribute
        // map; Generic_<rarity>_<Stat> names encode the stat). Prints id ->
        // names sorted by id.
        var map = new SortedDictionary<int, Dictionary<string,int>>();
        int scanned = 0;
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 106) continue;
            ParagonNodeDefinition n; try { n = d4.ReadParagonNode(e.Id); } catch { continue; }
            scanned++;
            // stat token = last underscore segment of a Generic_* node name
            string token = e.Name.StartsWith("Generic_", StringComparison.Ordinal)
                ? e.Name[(e.Name.LastIndexOf('_') + 1)..] : null!;
            if (token is null) continue;
            foreach (var a in n.Attributes)
            {
                if (!map.TryGetValue(a.AttributeId, out var d)) { d = new(); map[a.AttributeId] = d; }
                d.TryGetValue(token, out int c); d[token] = c + 1;
            }
        }
        Console.WriteLine($"scanned {scanned} ParagonNode defs; {map.Count} distinct AttributeIds with a Generic_* name token:");
        foreach (var kv in map)
        {
            var names = string.Join(", ", kv.Value.OrderByDescending(p => p.Value).Select(p => $"{p.Key}({p.Value})"));
            Console.WriteLine($"  attr {kv.Key,4} -> {names}");
        }
        return 0;
    }
    case "boardname":
    {
        if (argv.Count < 2) { Console.Error.WriteLine("boardname <boardSno...>"); return 2; }
        foreach (var s in argv.Skip(1))
        {
            int id = int.Parse(s);
            string nm = toc.Entries.Where(e => (int)e.Group == 108 && e.Id == id).Select(e => e.Name).FirstOrDefault() ?? "?";
            Console.WriteLine($"  {id,9}  {nm}  =  \"{(d4.TryReadParagonBoardName(id, out var dn) ? dn : "(no localized name)")}\"");
        }
        return 0;
    }
    case "nodesbyformula":
    {
        // nodesbyformula <formulaName> [warlock|all] — find all ParagonNodes
        // whose FormulaGbid == GbidHash(formulaName), then locate each on
        // Warlock paragon boards (or all boards). Used to build a precise read
        // list for empirical calibration of the budget-multiplier intrinsics.
        if (argv.Count < 2) { Console.Error.WriteLine("nodesbyformula <name> [warlock|all]"); return 2; }
        string scope = argv.Count > 2 ? argv[2] : "warlock";
        uint targetGbid = D4.GbidH(argv[1]);
        var nodes = new List<int>();
        foreach (var e in toc.Entries.Where(e => (int)e.Group == 106))
        {
            ParagonNodeDefinition pn; try { pn = d4.ReadParagonNode(e.Id); } catch { continue; }
            if (pn.Attributes.Any(a => a.FormulaGbid == targetGbid)) nodes.Add(e.Id);
        }
        Console.WriteLine($"formula \"{argv[1]}\" (gbid 0x{targetGbid:X8}) used by {nodes.Count} node defs.");
        var boards = toc.Entries.Where(e => (int)e.Group == 108
            && (scope == "all" || e.Name.Contains("Warlock", StringComparison.Ordinal))).ToList();
        foreach (var bsno in boards)
        {
            var bd = d4.ReadParagonBoard(bsno.Id);
            for (int row = 0; row < bd.Width; row++)
                for (int col = 0; col < bd.Width; col++)
                    if (bd.CellAt(row, col) is { } sn && nodes.Contains(sn))
                    {
                        var nm = toc.Entries.Where(t => (int)t.Group == 106 && t.Id == sn).Select(t => t.Name).FirstOrDefault() ?? "?";
                        Console.WriteLine($"  board {bsno.Name} ({bsno.Id}) row{row,2} col{col,2}  -> node {sn} \"{nm}\"");
                    }
        }
        return 0;
    }
    case "formulafind":
    {
        // formulafind <substr> — list AttributeFormulas entries whose name
        // contains substr (find the budget-multiplier intrinsics, if present).
        if (argv.Count < 2) { Console.Error.WriteLine("formulafind <substr>"); return 2; }
        var gbl = d4.ReadAttributeFormulas();
        int hit = 0;
        foreach (var e in gbl.Entries)
            if (e.Name.Contains(argv[1], StringComparison.OrdinalIgnoreCase))
            { Console.WriteLine($"  {e.Name} = \"{e.PrimaryText}\""); hit++; }
        Console.WriteLine($"-- {hit} of {gbl.Entries.Count} entries --");
        return 0;
    }
    case "formula":
    {
        // formula <name...> — resolve a formula's text from AttributeFormulas
        // (e.g. the ParagonPowerBudgetMultiplier* budget intrinsics).
        if (argv.Count < 2) { Console.Error.WriteLine("formula <name...>"); return 2; }
        var gbf = d4.ReadAttributeFormulas();
        foreach (var s in argv.Skip(1))
            Console.WriteLine(gbf.TryGetFormulaText(s, out var t) ? $"  {s} = \"{t}\"" : $"  {s} = (not in table)");
        return 0;
    }
    case "formuladump":
    {
        // LIB-3 R3: dump every AttributeFormulas entry with all ranges
        // (ItemPowerRangeStart, RangeValue1/2, FormulaText) to study the
        // function contracts + RangeValue clamp hypothesis. formuladump [substr]
        string sub = argv.Count > 1 ? argv[1] : "";
        var gbd = d4.ReadAttributeFormulas();
        int n = 0;
        var rv = new List<(float v1, float v2)>();
        foreach (var e in gbd.Entries)
        {
            if (sub.Length > 0 && !e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase)) continue;
            n++;
            if (sub.Length > 0)
            {
                Console.WriteLine($"{e.Name}  ({e.Ranges.Count} range(s))");
                foreach (var r in e.Ranges)
                    Console.WriteLine($"    ip>={r.ItemPowerRangeStart,4} clamp[{r.RangeValue1:0.####},{r.RangeValue2:0.####}]  \"{r.FormulaText}\"");
            }
            foreach (var r in e.Ranges) rv.Add((r.RangeValue1, r.RangeValue2));
        }
        // RangeValue clamp-hypothesis summary: how many distinct (v1,v2) pairs?
        var distinct = rv.Distinct().OrderBy(p => p.v1).ThenBy(p => p.v2).ToList();
        Console.WriteLine($"-- {n} entries; {rv.Count} ranges; {distinct.Count} distinct (RangeValue1,RangeValue2) pairs --");
        foreach (var p in distinct.Take(30)) Console.WriteLine($"    ({p.v1:0.####}, {p.v2:0.####}) x{rv.Count(x => x == p)}");
        return 0;
    }
    case "formulagbid":
    {
        // LIB-3 R2: resolve a GBID (e.g. an affix modifier idx16) via the
        // AttributeFormulas table and dump the full entry — every arRanges row
        // (ItemPowerRangeStart, RangeValue1/2, FormulaText). Tests whether the
        // affix idx16 keys the item-power value curve. formulagbid <hex...>
        if (argv.Count < 2) { Console.Error.WriteLine("formulagbid <hex...>"); return 2; }
        var gbg = d4.ReadAttributeFormulas();
        foreach (var s in argv.Skip(1))
        {
            uint g = Convert.ToUInt32(s.Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
            if (!gbg.TryGetNameByGbid(g, out var nm)) { Console.WriteLine($"  0x{g:X8} -> (not in AttributeFormulas)"); continue; }
            var entry = gbg.Entries.First(e => e.Name == nm);
            Console.WriteLine($"  0x{g:X8} -> {nm}  ({entry.Ranges.Count} range(s))");
            foreach (var rg in entry.Ranges)
                Console.WriteLine($"      itemPower>={rg.ItemPowerRangeStart,4}  v1={rg.RangeValue1:0.####}  v2={rg.RangeValue2:0.####}  \"{rg.FormulaText}\"");
        }
        return 0;
    }
    case "nodeinfo":
    {
        // nodeinfo <nodeSno...> — full decoded ParagonNode dump (dogfoods the
        // CL-66 fields): NodeType, rarity, flags, and per-attribute id/NParam/
        // formula(text)/AttributeGbid.
        if (argv.Count < 2) { Console.Error.WriteLine("nodeinfo <nodeSno...>"); return 2; }
        var gb = d4.ReadAttributeFormulas();
        foreach (var s in argv.Skip(1))
        {
            int sno = int.Parse(s);
            var nm = toc.Entries.Where(e => (int)e.Group == 106 && e.Id == sno).Select(e => e.Name).FirstOrDefault() ?? "?";
            var n = d4.ReadParagonNode(sno);
            Console.WriteLine($"node {sno} \"{nm}\"  NodeType={n.NodeType}({n.NodeTypeRaw}) Rarity={n.Rarity} gate={n.IsGate} socket={n.HasSocket} power={n.SnoPassivePower}");
            foreach (var a in n.Attributes)
            {
                string ftxt = a.IsInline ? $"inline\"{a.InlineFormula}\""
                    : (gb.TryGetNameByGbid(a.FormulaGbid, out var fn) && gb.TryGetFormulaText(fn, out var ft)
                        ? $"{fn}=\"{ft}\"" : $"gbid0x{a.FormulaGbid:X8}");
                Console.WriteLine($"    attr={a.AttributeId,5} nParam={a.NParam} +12={a.ParamPlus12} gbidArr=0x{a.AttributeGbid:X8} formula={ftxt}");
            }
        }
        return 0;
    }
    case "cellof":
    {
        // cellof <boardSno> <nodeSno...> — print every (row,col) where each node
        // appears on the board grid (row 0 = top, col 0 = left, row-major).
        if (argv.Count < 3) { Console.Error.WriteLine("cellof <boardSno> <nodeSno...>"); return 2; }
        var bd = d4.ReadParagonBoard(int.Parse(argv[1]));
        var want = argv.Skip(2).Select(int.Parse).ToHashSet();
        for (int row = 0; row < bd.Width; row++)
            for (int col = 0; col < bd.Width; col++)
                if (bd.CellAt(row, col) is { } sn && want.Contains(sn))
                    Console.WriteLine($"  node {sn} at (row {row}, col {col})  [row 0=top, col 0=left]");
        return 0;
    }
    case "boardnodes":
    {
        // boardnodes <boardSno> [max] — for each distinct cell node of a paragon
        // board, print its kind flags + HIcon/HIconMask + the HIconMask's native
        // frame size. Finds the class/start node's emblem + its authored size.
        if (argv.Count < 2) { Console.Error.WriteLine("boardnodes <boardSno> [max]"); return 2; }
        int bsno = int.Parse(argv[1]); int max = argv.Count > 2 ? int.Parse(argv[2]) : 60;
        var board = d4.ReadParagonBoard(bsno);
        var seen = new HashSet<int>(); int shown = 0;
        foreach (var cell in board.Cells)
        {
            if (cell is not { } nodeSno || !seen.Add(nodeSno)) continue;
            ParagonNodeDefinition n; try { n = d4.ReadParagonNode(nodeSno); } catch { continue; }
            int nodeType = d4.TryReadSno(106, nodeSno, SnoFolder.Meta, out var nb) && nb.Length >= 0x24
                ? BitConverter.ToInt32(nb, 0x20) : -999;   // payload+16 (undecoded field)
            string mask = "-";
            if (n.HIconMask != 0 && d4.Catalog.TryResolveFrame(n.HIconMask, out var atlas, out var fr)
                && d4.Catalog.TryPeek(atlas, out var f) && f.Width is { } aw && f.Height is { } ah)
            {
                var (_, _, pw, ph) = fr.PixelRect(aw, ah);
                mask = $"0x{n.HIconMask:X8} {pw}x{ph} in '{atlas.Name}'";
            }
            else if (n.HIconMask != 0) mask = $"0x{n.HIconMask:X8} (unresolved)";
            string attrs = n.Attributes.Count == 0 ? "(none)" : string.Join("; ", n.Attributes.Select(a =>
                $"attr={a.AttributeId} nParam={a.NParam} +12={a.ParamPlus12} " +
                (a.IsInline ? $"inline=\"{a.InlineFormula}\"" : $"gbid=0x{a.FormulaGbid:X8}")));
            Console.WriteLine($"node {nodeSno,9} type@16={nodeType,-2} gate={n.IsGate} socket={n.HasSocket} rar={n.RarityOverride}");
            Console.WriteLine($"      attrs: {attrs}");
            if (++shown >= max) break;
        }
        return 0;
    }
    case "snoid":
    {
        // snoid <id...> — find any CoreTOC entry by exact id (across all groups).
        if (argv.Count < 2) { Console.Error.WriteLine("snoid <id...>"); return 2; }
        var want = argv.Skip(1).Select(int.Parse).ToHashSet();
        foreach (var e in toc.Entries.Where(e => want.Contains(e.Id)))
            Console.WriteLine($"  group {(int)e.Group,3}  id {e.Id,9}  {e.Name}");
        return 0;
    }
    case "strdump":
    {
        // Generic recon: dump every printable ASCII run (>=minlen) in ANY SNO
        // payload, with payload offset — for GameBalance / monster-table RE.
        //   strdump <group> <sno> [minlen=3]
        if (argv.Count < 3) { Console.Error.WriteLine("strdump <group> <sno> [minlen]"); return 2; }
        int g = int.Parse(argv[1]), s = int.Parse(argv[2]);
        int minLen = argv.Count > 3 ? int.Parse(argv[3]) : 3;
        int pb = SnoRecord.DefaultPayloadBase;
        if (!d4.TryReadSno((SnoGroup)g, s, SnoFolder.Meta, out var b)) { Console.WriteLine("no content"); return 1; }
        Console.WriteLine($"## {g}/{s} len={b.Length}");
        int i = pb;
        while (i < b.Length)
        {
            if (b[i] is >= 0x20 and < 0x7f)
            {
                int start = i;
                while (i < b.Length && b[i] is >= 0x20 and < 0x7f) i++;
                int n = i - start;
                if (n >= minLen)
                    Console.WriteLine($"   +{start - pb,-5} \"{System.Text.Encoding.ASCII.GetString(b, start, n)}\"");
            }
            else i++;
        }
        return 0;
    }
    case "rawhex":
    {
        // rawhex <group> <sno> [off=0] [len=256] — payload-relative hex+u32 dump
        // (payload base 0x10) to verify undecoded SNO record fields.
        if (argv.Count < 3) { Console.Error.WriteLine("rawhex <group> <sno> [off] [len]"); return 2; }
        int g = int.Parse(argv[1]), s = int.Parse(argv[2]);
        int off = argv.Count > 3 ? int.Parse(argv[3]) : 0;
        int len = argv.Count > 4 ? int.Parse(argv[4]) : 256;
        if (!d4.TryReadSno((SnoGroup)g, s, SnoFolder.Meta, out var blob)) { Console.Error.WriteLine("not found"); return 1; }
        int pbase = 0x10;
        for (int p = off; p < off + len && pbase + p + 4 <= blob.Length; p += 4)
            Console.WriteLine($"  payload+{p,-4} (0x{pbase + p:X4})  u32={BitConverter.ToUInt32(blob, pbase + p),12}  i32={BitConverter.ToInt32(blob, pbase + p),12}  f32={BitConverter.ToSingle(blob, pbase + p),14:0.###}  hex={BitConverter.ToUInt32(blob, pbase + p):X8}");
        return 0;
    }
    case "findhandle":
    {
        // findhandle <hex> [hex...] — scan EVERY group-44 texture's frames for
        // the handle (broader than the icon-frame index), to locate art the
        // icon catalog doesn't index (e.g. ContextualHighlight_Square pieces).
        if (argv.Count < 2) { Console.Error.WriteLine("findhandle <hex> [hex...]"); return 2; }
        var targets = new HashSet<uint>();
        for (int a = 1; a < argv.Count; a++)
            targets.Add(Convert.ToUInt32(argv[a].Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16));
        int scanned = 0; var found = new HashSet<uint>();
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 44) continue;
            if (!d4.TextureMeta.TryGet(e.Id, out var td)) continue;
            scanned++;
            for (int i = 0; i < td.Frames.Count; i++)
                if (targets.Contains(td.Frames[i].ImageHandle))
                {
                    var (x, y, w, h) = td.Frames[i].PixelRect(td.Width, td.Height);
                    Console.WriteLine($"0x{td.Frames[i].ImageHandle:X8} -> atlas {e.Id} '{e.Name}' {td.Codec} frame[{i}] {w}x{h} @ ({x},{y})");
                    found.Add(td.Frames[i].ImageHandle);
                }
        }
        foreach (var t in targets) if (!found.Contains(t)) Console.WriteLine($"0x{t:X8} -> NOT FOUND in {scanned} group-44 textures");
        return 0;
    }
    case "framesize":
    {
        // framesize <hex> [hex...] — resolve each texture handle to its owning
        // atlas + native pixel size (via d4.Catalog), to ground node-rect
        // "oversized" questions (does an all-zero rect mean native size?).
        if (argv.Count < 2) { Console.Error.WriteLine("framesize <hex> [hex...]"); return 2; }
        for (int a = 1; a < argv.Count; a++)
        {
            uint h = Convert.ToUInt32(argv[a].Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
            if (d4.Catalog.TryResolveFrame(h, out var atlas, out var fr) &&
                d4.Catalog.TryPeek(atlas, out var f) && f.Width is { } aw && f.Height is { } ah)
            {
                var (x, y, pw, ph) = fr.PixelRect(aw, ah);
                Console.WriteLine($"0x{h:X8} -> atlas {atlas.Sno} '{atlas.Name}' {f.Codec} {aw}x{ah}; frame native {pw}x{ph} @ ({x},{y})");
            }
            else Console.WriteLine($"0x{h:X8} -> unresolved (not an atlas frame)");
        }
        return 0;
    }
    case "widgetdump":
    {
        // widgetdump <sceneSno> <nameSubstr> — via the PARSED UiScene, print
        // each matching widget's own hImageFrame + rect + bActive + anchoring,
        // and each handle-bearing child likewise. Grounds node-recipe geometry
        // questions (e.g. socket disc on Template_Node_Socketable vs Usage_Slot_2).
        if (argv.Count < 3) { Console.Error.WriteLine("widgetdump <sceneSno> <nameSubstr>"); return 2; }
        int sc = int.Parse(argv[1]); string sub = argv[2];
        uint FH(string n) => Diablo4.FieldHash(n);
        uint hImg = FH("hImageFrame");
        (string, uint)[] rectF = [("L", FH("nLeft")), ("R", FH("nRight")), ("T", FH("nTop")),
            ("B", FH("nBottom")), ("W", FH("nWidth")), ("H", FH("nHeight"))];
        uint bAct = FH("bActive"), vAnc = FH("eVerticalAnchoring"), hAnc = FH("eHorizontalAnchoring"), tint = FH("rgbaTint");
        long V(IReadOnlyList<UiField> fs, uint h) { foreach (var f in fs) if (f.FieldHash == h && f.HasValue) return (int)f.RawValue; return 0; }
        bool Has(IReadOnlyList<UiField> fs, uint h) { foreach (var f in fs) if (f.FieldHash == h && f.HasValue) return true; return false; }
        string Line(IReadOnlyList<UiField> fs)
        {
            var rect = string.Join(",", rectF.Select(rf => $"{rf.Item1}={V(fs, rf.Item2)}"));
            string b = Has(fs, bAct) ? V(fs, bAct).ToString() : "(unset=1)";
            string a = $"vAnc={(Has(fs, vAnc) ? V(fs, vAnc).ToString() : "-")} hAnc={(Has(fs, hAnc) ? V(fs, hAnc).ToString() : "-")}";
            string ti = Has(fs, tint) ? $" tint=0x{(uint)V(fs, tint):X8}" : "";
            return $"img=0x{(uint)V(fs, hImg):X8} [{rect}] bActive={b} {a}{ti}";
        }
        var scene = d4.ReadUiScene(sc);
        foreach (var w in scene.Widgets)
        {
            if (!(w.Name ?? "").Contains(sub, StringComparison.OrdinalIgnoreCase)) continue;
            Console.WriteLine($"'{w.Name}'  {Line(w.Fields)}  children={w.Children.Count}");
            for (int c = 0; c < w.Children.Count; c++)
                Console.WriteLine($"    [{c}] {Line(w.Children[c].Fields)}");
        }
        return 0;
    }
    case "codecscan":
    {
        // Tally the texture codec across all UI atlases (2DUI*, group 44) to
        // size the decode-coverage work for a UI atlas catalog.
        var byCodec = new Dictionary<string, (int atlases, long frames)>();
        int total = 0, noMeta = 0;
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 44 || !e.Name.StartsWith("2DUI", StringComparison.OrdinalIgnoreCase)) continue;
            total++;
            if (d4.TextureMeta.TryGet(e.Id, out var td))
            {
                var c = td.Codec.ToString();
                byCodec.TryGetValue(c, out var v);
                byCodec[c] = (v.atlases + 1, v.frames + td.Frames.Count);
            }
            else noMeta++;
        }
        Console.WriteLine($"UI atlases (2DUI*, group 44): {total}  (no combined-meta: {noMeta})");
        Console.WriteLine($"{"codec",-14} {"atlases",8} {"frames",10}");
        foreach (var kv in byCodec.OrderByDescending(k => k.Value.atlases))
            Console.WriteLine($"{kv.Key,-14} {kv.Value.atlases,8} {kv.Value.frames,10}");
        return 0;
    }
    case "texdump":
    {
        // texdump <sno> [offset=0] [len=...] — locate the texture's
        // combined-meta entry and hex-dump its raw bytes (relative to the
        // entry's descStart, with the parsed fields annotated). For the
        // FR-C20 #32 codec-tail investigation.
        if (argv.Count < 2) { Console.Error.WriteLine("texdump <sno> [offset] [len]"); return 2; }
        int sno = int.Parse(argv[1]);
        int dumpOff = argv.Count > 2 ? int.Parse(argv[2]) : 0;
        int dumpLen = argv.Count > 3 ? int.Parse(argv[3]) : 256;

        // Re-walk the bundle to locate this SNO's entry — same shape as
        // CombinedTextureMeta.Parse uses internally.
        var bundle = d4.Casc.ReadPath(@"Base\Texture-Base-Global.dat");
        var count = (int)BitConverter.ToUInt32(bundle, 4);
        var indices = new (int Sno, int Size)[count];
        for (int i = 0; i < count; i++)
        {
            indices[i].Sno = BitConverter.ToInt32(bundle, 8 + i * 8);
            indices[i].Size = (int)BitConverter.ToUInt32(bundle, 12 + i * 8);
        }
        int cursor = 8 + count * 8;
        int hitDesc = -1, hitSize = 0;
        foreach (var (s, size) in indices)
        {
            int aligned = (cursor + 7) & ~7;
            int descStart = aligned + 8;
            if (descStart + size > bundle.Length) break;
            int entrySno = BitConverter.ToInt32(bundle, descStart);
            if (entrySno == sno) { hitDesc = descStart; hitSize = size; break; }
            cursor = descStart + size;
        }
        if (hitDesc < 0) { Console.Error.WriteLine($"sno {sno} not in bundle"); return 1; }

        int payloadBase = hitDesc + 4;
        int payloadEnd = hitDesc + hitSize;
        Console.WriteLine($"sno {sno}  descStart 0x{hitDesc:X8}  size {hitSize}  payloadBase 0x{payloadBase:X8}  payloadEnd 0x{payloadEnd:X8}");
        for (int p = dumpOff; p < dumpOff + dumpLen && payloadBase + p + 4 <= payloadEnd; p += 4)
        {
            uint u = BitConverter.ToUInt32(bundle, payloadBase + p);
            int sI = unchecked((int)u);
            float f = BitConverter.UInt32BitsToSingle(u);
            // annotate known fields
            string note = p switch
            {
                0  => "  <- snoId (descStart+0 also)",
                8  => "  <- eTexFormat",
                16 => "  <- nWidth (u16 lo) | nHeight (u16 hi)",
                24 => "  <- nMipMin/Max packed",
                64 => "  <- serTex DT_VARIABLEARRAY",
                80 => "  <- ptFrame DT_VARIABLEARRAY",
                _  => ""
            };
            Console.WriteLine($"  payload+{p,-4} (0x{payloadBase + p:X4})  u32={u,12}  i32={sI,12}  f32={f,14:0.###}  hex={u:X8}{note}");
        }
        return 0;
    }
    case "listframes":
    {
        // listframes <atlasSno> — list every TexFrame in an atlas
        // (handle + native pixel rect at mip0). For finding chrome
        // pieces not surfaced via any TiledStyle 9-slice.
        if (argv.Count < 2) { Console.Error.WriteLine("listframes <atlasSno>"); return 2; }
        int sno = int.Parse(argv[1]);
        if (!d4.TextureMeta.TryGet(sno, out var td))
        { Console.Error.WriteLine($"no combined-meta for atlas {sno}"); return 1; }
        Console.WriteLine($"atlas {sno} {td.Codec} {td.Width}x{td.Height}  frames={td.Frames.Count}");
        int idx = 0;
        foreach (var f in td.Frames)
        {
            var (x, y, w, h) = f.PixelRect(td.Width, td.Height);
            Console.WriteLine($"  [{idx,3}] 0x{f.ImageHandle:X8}  {w,4}x{h,-4} @ ({x,4},{y,4})  inner={(f.HasDistinctInner ? "yes" : "no")}");
            idx++;
        }
        return 0;
    }
    case "framescan":
    {
        // framescan — scan every TextureDefinition in the combined-meta and
        // check whether the per-frame 16-byte tail (+20..+35) equals the
        // primary UV rect (+4..+19). Reports any frame where they differ
        // (the FR-C20 #32 codec-tail question: is the tail a duplicate?).
        var bundle = d4.Casc.ReadPath(@"Base\Texture-Base-Global.dat");
        var count = (int)BitConverter.ToUInt32(bundle, 4);
        var idx = new (int Sno, int Size)[count];
        for (int i = 0; i < count; i++)
        {
            idx[i].Sno = BitConverter.ToInt32(bundle, 8 + i * 8);
            idx[i].Size = (int)BitConverter.ToUInt32(bundle, 12 + i * 8);
        }
        int cursor = 8 + count * 8;
        int scanned = 0, divergent = 0, divergentFrames = 0;
        foreach (var (s, size) in idx)
        {
            int aligned = (cursor + 7) & ~7;
            int descStart = aligned + 8;
            cursor = descStart + size;
            if (descStart + size > bundle.Length) break;
            int payloadBase = descStart + 4;
            int frDescOff = payloadBase + 80;
            int frDataOff = BitConverter.ToInt32(bundle, frDescOff + 4);
            int frDataSize = BitConverter.ToInt32(bundle, frDescOff + 8);
            if (frDataSize <= 0) continue;
            int frStart = descStart + frDataOff;
            int frCount = frDataSize / 36;
            if (frStart + frCount * 36 > bundle.Length) continue;
            scanned++;
            int snoDiv = 0;
            for (int i = 0; i < frCount; i++)
            {
                int b = frStart + i * 36;
                bool eq = true;
                for (int k = 0; k < 16; k++)
                    if (bundle[b + 4 + k] != bundle[b + 20 + k]) { eq = false; break; }
                if (!eq) { snoDiv++; divergentFrames++; }
            }
            if (snoDiv > 0) divergent++;
        }
        Console.WriteLine($"scanned {scanned} TextureDefinitions with frames; {divergent} atlases with tail != primary UV ({divergentFrames} divergent frames)");
        return 0;
    }
    case "framediv":
    {
        // framediv [max=5] — list a sample of divergent atlases + show the
        // first divergent frame's primary UV vs tail UV side by side.
        int max = argv.Count > 1 ? int.Parse(argv[1]) : 5;
        var bundle = d4.Casc.ReadPath(@"Base\Texture-Base-Global.dat");
        var count = (int)BitConverter.ToUInt32(bundle, 4);
        var idx = new (int Sno, int Size)[count];
        for (int i = 0; i < count; i++)
        {
            idx[i].Sno = BitConverter.ToInt32(bundle, 8 + i * 8);
            idx[i].Size = (int)BitConverter.ToUInt32(bundle, 12 + i * 8);
        }
        int cursor = 8 + count * 8;
        int shown = 0;
        foreach (var (s, size) in idx)
        {
            if (shown >= max) break;
            int aligned = (cursor + 7) & ~7;
            int descStart = aligned + 8;
            cursor = descStart + size;
            if (descStart + size > bundle.Length) break;
            int payloadBase = descStart + 4;
            int frDescOff = payloadBase + 80;
            int frDataOff = BitConverter.ToInt32(bundle, frDescOff + 4);
            int frDataSize = BitConverter.ToInt32(bundle, frDescOff + 8);
            if (frDataSize <= 0) continue;
            int frStart = descStart + frDataOff;
            int frCount = frDataSize / 36;
            if (frStart + frCount * 36 > bundle.Length) continue;
            for (int i = 0; i < frCount; i++)
            {
                int b = frStart + i * 36;
                bool eq = true;
                for (int k = 0; k < 16; k++)
                    if (bundle[b + 4 + k] != bundle[b + 20 + k]) { eq = false; break; }
                if (eq) continue;
                var name = toc.Entries.Where(e => (int)e.Group == 44 && e.Id == s).Select(e => e.Name).FirstOrDefault() ?? "?";
                int format = (int)BitConverter.ToUInt32(bundle, payloadBase + 8);
                int w = BitConverter.ToUInt16(bundle, payloadBase + 16);
                int h = BitConverter.ToUInt16(bundle, payloadBase + 18);
                Console.WriteLine($"sno {s,8} fmt={format} {w}x{h} frame[{i}]/{frCount} '{name}'");
                Console.Write($"  primary:  uv=");
                for (int k = 4; k < 20; k += 4) Console.Write($" {BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(bundle, b + k)):0.####}");
                Console.WriteLine();
                Console.Write($"  tail:     uv=");
                for (int k = 20; k < 36; k += 4) Console.Write($" {BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(bundle, b + k)):0.####}");
                Console.WriteLine();
                Console.Write($"  tail hex:    ");
                for (int k = 20; k < 36; k += 4) Console.Write($"  +{k}=0x{BitConverter.ToUInt32(bundle, b + k):X8}");
                Console.WriteLine();
                shown++;
                break;
            }
        }
        return 0;
    }
    case "frametail":
    {
        // frametail <sno> — list each TexFrame's raw 36-byte slice for one atlas,
        // surfacing the 16 trailing bytes after (u,v0,u,v1) so the codec-tail
        // investigation can see whether they carry layer/flag/anim data.
        if (argv.Count < 2) { Console.Error.WriteLine("frametail <sno> [maxFrames]"); return 2; }
        int sno = int.Parse(argv[1]);
        int max = argv.Count > 2 ? int.Parse(argv[2]) : 4;

        var bundle = d4.Casc.ReadPath(@"Base\Texture-Base-Global.dat");
        var count = (int)BitConverter.ToUInt32(bundle, 4);
        var indices = new (int Sno, int Size)[count];
        for (int i = 0; i < count; i++)
        {
            indices[i].Sno = BitConverter.ToInt32(bundle, 8 + i * 8);
            indices[i].Size = (int)BitConverter.ToUInt32(bundle, 12 + i * 8);
        }
        int cursor = 8 + count * 8;
        int hitDesc = -1, hitSize = 0;
        foreach (var (s, size) in indices)
        {
            int aligned = (cursor + 7) & ~7;
            int descStart = aligned + 8;
            if (descStart + size > bundle.Length) break;
            int entrySno = BitConverter.ToInt32(bundle, descStart);
            if (entrySno == sno) { hitDesc = descStart; hitSize = size; break; }
            cursor = descStart + size;
        }
        if (hitDesc < 0) { Console.Error.WriteLine($"sno {sno} not in bundle"); return 1; }

        int payloadBase = hitDesc + 4;
        int frDescOff = payloadBase + 80;
        int frDataOff = BitConverter.ToInt32(bundle, frDescOff + 4);
        int frDataSize = BitConverter.ToInt32(bundle, frDescOff + 8);
        int frStart = hitDesc + frDataOff;
        int frCount = frDataSize / 36;
        Console.WriteLine($"sno {sno}  frames={frCount}  (size={frDataSize})");
        for (int i = 0; i < Math.Min(frCount, max); i++)
        {
            int b = frStart + i * 36;
            uint hdl = BitConverter.ToUInt32(bundle, b);
            float u0 = BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(bundle, b + 4));
            float v0 = BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(bundle, b + 8));
            float u1 = BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(bundle, b + 12));
            float v1 = BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(bundle, b + 16));
            Console.WriteLine($"  frame[{i}] handle=0x{hdl:X8} uv=({u0:0.####},{v0:0.####})-({u1:0.####},{v1:0.####})");
            Console.Write($"    tail bytes @+20..+35:");
            for (int k = 20; k < 36; k += 4)
                Console.Write($"  +{k}=0x{BitConverter.ToUInt32(bundle, b + k):X8}");
            Console.WriteLine();
            // also display as floats for the tail to spot any UV/scale values
            Console.Write("                    as float:");
            for (int k = 20; k < 36; k += 4)
            {
                float v = BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(bundle, b + k));
                Console.Write($"  +{k}={v,10:0.####}");
            }
            Console.WriteLine();
        }
        return 0;
    }
    case "nodetuplescan":
    {
        // FR-C28 recon: enumerate every (AttributeId, ParamPlus12) tuple
        // on group-106 ParagonNode records where ParamPlus12 != -1 (a
        // tag-conditional ptAttributes entry).  ptAttributes lives at the
        // payload+32 DT_VARIABLEARRAY descriptor (dataOffset@+8,
        // dataSize@+12); element stride is 88 bytes — AttributeId is at
        // entry+0, ParamPlus12 is at entry+12 (per the NodeAttribute
        // shape in src/.../ParagonNodeDefinition.cs).
        const int GidNode = 106;
        const int Stride = 88;
        const int DescriptorOff = 32;
        const int ParamFieldOff = 12;
        Console.WriteLine("attr\tparam\tnode_sno\tnode_name");
        var entries = toc.Entries.Where(e => (int)e.Group == GidNode);
        foreach (var e in entries)
        {
            if (!d4.TryReadSno(GidNode, e.Id, SnoFolder.Meta, out var b)) continue;
            const int PB = 0x10;
            if (b.Length < PB + DescriptorOff + 16) continue;
            int dataOff = BitConverter.ToInt32(b, PB + DescriptorOff + 8);
            int dataSize = BitConverter.ToInt32(b, PB + DescriptorOff + 12);
            if (dataOff <= 0 || dataSize <= 0 || dataSize % Stride != 0) continue;
            int count = dataSize / Stride;
            if (PB + dataOff + dataSize > b.Length) continue;
            for (int i = 0; i < count; i++)
            {
                int entryBase = PB + dataOff + i * Stride;
                int attr = BitConverter.ToInt32(b, entryBase);
                uint param = BitConverter.ToUInt32(b, entryBase + ParamFieldOff);
                if (param == 0xFFFFFFFFu) continue;
                Console.WriteLine($"{attr}\t0x{param:X8}\t{e.Id}\t{e.Name}");
            }
        }
        return 0;
    }
    case "multcheck":
    {
        // FR-C31 R2: test the "multiplicative variant id = additive id + 1"
        // convention. For each group-112 ParagonGlyphAffix whose name contains
        // "Mult", print each AffectedAttribute id, GetAttributeName(id) and
        // GetAttributeName(id-1). If id resolves to null/wrong but (id-1)
        // matches the affix's stat, the +1 convention holds.
        //   multcheck [substr=Mult] [max=400]
        string sub = argv.Count > 1 ? argv[1] : "Mult";
        int max = argv.Count > 2 ? int.Parse(argv[2]) : 400;
        int total = 0, idNull = 0, prevResolves = 0;
        foreach (var e in toc.Entries.Where(e => (int)e.Group == 112
                     && e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase))
                 .OrderBy(e => e.Name).Take(max))
        {
            ParagonGlyphAffixDefinition gx; try { gx = d4.ReadParagonGlyphAffix(e.Id); } catch { continue; }
            foreach (var ar in gx.AffectedAttributes)
            {
                if (ar.AttributeId < 0) continue;   // DataAttributes namespace — separate
                total++;
                string nId = d4.GetAttributeName(ar.AttributeId) ?? "(null)";
                string nPrev = d4.GetAttributeName(ar.AttributeId - 1) ?? "(null)";
                if (nId == "(null)") idNull++;
                if (nId == "(null)" && nPrev != "(null)") prevResolves++;
                Console.WriteLine($"{e.Name,-52} id={ar.AttributeId,4} name(id)={nId,-28} name(id-1)={nPrev}");
            }
        }
        Console.WriteLine($"-- {total} attrs; id null={idNull}; of those, (id-1) resolves={prevResolves} --");
        return 0;
    }
    case "glyphaffixscan":
    {
        // FR-C24 slice 2b recon: dump candidate slice-2b offsets for every
        // group-112 ParagonGlyphAffix.  Columns chosen from devlog 0078's
        // single-affix dump (DamageWhileHealthy_Intelligence_Side @1068542).
        //   +24  : eAffectedNodeRarity (DT_ENUM)            int32
        //   +48  : eBonusOperation (DT_ENUM)                int32
        //   +64,+68 : DT_VARIABLEARRAY descriptor (dataOff, dataSize)
        //   +72  : SkillTagSelector candidate               u32 (GBID-shaped)
        //   +76  : flStartingBonusScalar (Base)             float
        //   +80  : flAddedBonusScalarPerLevel (PerLevel)    float
        //   +84  : DisplayFactor / mystery float            float
        //   +88..+96 : observed-zero on the seed dump       u32×3
        //   +120,+124: 2nd DT_VARIABLEARRAY descriptor      (dataOff, dataSize)
        //   payload length + first VLA payload + second VLA payload tail.
        // Output is tab-separated; pipe to a file to import into a sheet.
        int gid = 112;
        string sub = argv.Count > 1 ? argv[1] : "";
        int max = argv.Count > 2 ? int.Parse(argv[2]) : 400;
        Console.WriteLine("sno\tname\tlen\tar+24\top+48\tva1_off+64\tva1_size+68\tgbid+72\tbase+76\tper+80\tf+84\tu+88\tu+92\tu+96\tva2_off+120\tva2_size+124\tva1_payload_hex\tva2_payload_hex");
        var picks = toc.Entries.Where(e => (int)e.Group == gid
                && e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name).Take(max);
        foreach (var e in picks)
        {
            if (!d4.TryReadSno(gid, e.Id, SnoFolder.Meta, out var b)) continue;
            const int PB = 0x10; // payload base
            int P(int o) => PB + o;
            int Lim = b.Length;
            int I32(int o) => P(o) + 4 <= Lim ? BitConverter.ToInt32(b, P(o)) : 0;
            uint U32(int o) => (uint)(P(o) + 4 <= Lim ? BitConverter.ToInt32(b, P(o)) : 0);
            float F32(int o) => P(o) + 4 <= Lim ? BitConverter.UInt32BitsToSingle(BitConverter.ToUInt32(b, P(o))) : 0f;
            int va1Off = I32(64), va1Size = I32(68);
            int va2Off = I32(120), va2Size = I32(124);
            string Hex(int payOff, int size)
            {
                if (size <= 0 || size > 256) return "";
                int start = P(payOff);
                if (start < 0 || start + size > Lim) return "";
                var sb = new System.Text.StringBuilder(size * 2 + 8);
                for (int k = 0; k < size; k += 4)
                    sb.Append(BitConverter.ToUInt32(b, start + k).ToString("X8")).Append(' ');
                return sb.ToString().TrimEnd();
            }
            Console.WriteLine(string.Join('\t',
                e.Id,
                e.Name,
                b.Length,
                I32(24),
                I32(48),
                va1Off, va1Size,
                $"0x{U32(72):X8}",
                F32(76).ToString("0.######"),
                F32(80).ToString("0.######"),
                F32(84).ToString("0.######"),
                $"0x{U32(88):X8}",
                $"0x{U32(92):X8}",
                $"0x{U32(96):X8}",
                va2Off, va2Size,
                Hex(va1Off, va1Size),
                Hex(va2Off, va2Size)));
        }
        return 0;
    }
    case "coverfix":
    {
        // FR-C27 R2: measure the affix-Desc source's coverage contribution.
        // Build idx4 -> Desc-token from single-modifier g104 affixes; for every
        // live positive attribute id referenced by nodes (g106 Generic_) +
        // glyph affixes (g112), report resolves-now (GetAttributeName) vs
        // resolves-via-affix-Desc (idx4 -> token -> sno-4080 localize).
        int pb = SnoRecord.DefaultPayloadBase;
        var affixToken = new Dictionary<int, string>();
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 104) continue;
            if (!d4.TryReadSno(104, e.Id, SnoFolder.Meta, out var b)) continue;
            var r = new SnoRecord(b);
            if (pb + 0xB0 + 8 > b.Length) continue;
            int mo = r.I32(0xB0), ms = r.I32(0xB4);
            if (mo <= 0 || ms != 104 || pb + mo + ms > b.Length) continue;   // single-modifier only
            int attr = r.I32(mo + 16);
            if (attr <= 0) continue;
            string desc = ""; try { desc = d4.ReadAffix(e.Id).Description; } catch { }
            int lb = desc.IndexOf('[');
            if (lb < 0) continue;
            int j = lb + 1; while (j < desc.Length && desc[j] == ' ') j++;
            int st = j; while (j < desc.Length && (char.IsLetterOrDigit(desc[j]) || desc[j] == '_')) j++;
            string tok = desc.Substring(st, j - st);
            if (tok.Length < 3 || char.IsDigit(tok[0])) continue;
            affixToken.TryAdd(attr, tok);
        }
        var sl = d4.GetStrings();
        string Loc(string tok) => sl.TryGet(4080, tok, out var t) ? AttributeNames.StripTemplate(t) : "";
        // enumerate live positive attr ids from nodes + glyph affixes
        var ids = new HashSet<int>();
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group == 106 && e.Name.StartsWith("Generic_", StringComparison.Ordinal))
            { try { foreach (var a in d4.ReadParagonNode(e.Id).Attributes) if (a.AttributeId > 0) ids.Add(a.AttributeId); } catch { } }
            else if ((int)e.Group == 112)
            { try { foreach (var a in d4.ReadParagonGlyphAffix(e.Id).AffectedAttributes) if (a.AttributeId > 0) ids.Add(a.AttributeId); } catch { } }
        }
        int now = 0, viaAffix = 0, combined = 0, affixOnly = 0;
        foreach (var id in ids)
        {
            bool rn = d4.GetAttributeName(id) != null;
            bool va = affixToken.TryGetValue(id, out var tk) && Loc(tk).Length > 0;
            if (rn) now++; if (va) viaAffix++;
            if (rn || va) combined++;
            if (!rn && va) { affixOnly++; if (affixOnly <= 20) Console.WriteLine($"  +{id} -> {Loc(affixToken[id])}  (affix token {affixToken[id]})"); }
        }
        Console.WriteLine($"-- live positive attr ids (node+glyph): {ids.Count} | resolves now: {now} ({100.0*now/ids.Count:0.#}%) | affix-Desc alone: {viaAffix} | COMBINED: {combined} ({100.0*combined/ids.Count:0.#}%) | affix-Desc rescues {affixOnly} the node scan misses --");
        Console.WriteLine($"-- affix-Desc id->token map size: {affixToken.Count} --");
        return 0;
    }
    case "attrname":
    {
        // FR-C27: probe Diablo4Storage.GetAttributeName(id[,param]) — the
        // runtime resolver. attrname <id> [param]
        if (argv.Count < 2) { Console.Error.WriteLine("attrname <id> [param]"); return 2; }
        int id = int.Parse(argv[1]);
        if (argv.Count > 2)
        {
            uint p = argv[2].StartsWith("0x") ? Convert.ToUInt32(argv[2], 16) : uint.Parse(argv[2]);
            Console.WriteLine($"  {id} (param 0x{p:X8}) -> {d4.GetAttributeName(id, p) ?? "(null)"}");
        }
        else
        {
            string data = d4.TryGetDataAttributeName(id, out var dn) ? $"  [DataAttr: {dn}]" : "";
            Console.WriteLine($"  {id} -> {d4.GetAttributeName(id) ?? "(null)"}{data}");
        }
        return 0;
    }
    case "attrcover":
    {
        // FR-C27 coverage validation: build AttributeId -> name-token by
        // scanning live records (season-robust source): ParagonNode
        // Generic_<Rarity>_<Token> names + ParagonGlyphAffix names via their
        // AffectedAttributes. Prints id -> tokens (top 3). Recon only.
        var map = new SortedDictionary<int, Dictionary<string,int>>();
        void Add(int id, string tok)
        {
            if (string.IsNullOrEmpty(tok)) return;
            if (!map.TryGetValue(id, out var d)) { d = new(); map[id] = d; }
            d.TryGetValue(tok, out int c); d[tok] = c + 1;
        }
        int nodes = 0, affixes = 0;
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 106) continue;
            ParagonNodeDefinition n; try { n = d4.ReadParagonNode(e.Id); } catch { continue; }
            if (!e.Name.StartsWith("Generic_", StringComparison.Ordinal)) continue;
            var parts = e.Name.Split('_');
            if (parts.Length < 3) continue;
            string tok = string.Join("_", parts.Skip(2));   // after Generic_<Rarity>_
            nodes++;
            foreach (var a in n.Attributes) Add(a.AttributeId, tok);
        }
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 112) continue;
            ParagonGlyphAffixDefinition gx; try { gx = d4.ReadParagonGlyphAffix(e.Id); } catch { continue; }
            affixes++;
            foreach (var ar in gx.AffectedAttributes) Add(ar.AttributeId, "affix:" + e.Name);
        }
        Console.WriteLine($"scanned {nodes} Generic_ nodes + {affixes} glyph affixes; {map.Count} distinct AttributeIds:");
        foreach (var kv in map)
            Console.WriteLine($"  attr {kv.Key,4} -> {string.Join(" | ", kv.Value.OrderByDescending(p => p.Value).Take(3).Select(p => p.Key))}");
        return 0;
    }
    case "dataattrs":
    {
        // FR-C27 recon: dump DataAttributes (sno 1907204) entries. Header at
        // payload+80/+84 = VLA (dataOff, dataSize); entries are `stride`-byte
        // records: szName@+0 (ASCII), gbid@+256, ~100 bytes aux @+260..+360.
        // With a filter substring, dumps every int32 @+256..+356 for matches
        // (to correlate the AttributeId field offset against known ids).
        //   dataattrs [namefilter] [stride=360]
        string filt = argv.Count > 1 ? argv[1] : "";
        int stride = argv.Count > 2 ? int.Parse(argv[2]) : 360;
        if (!d4.TryReadSno((SnoGroup)20, 1907204, SnoFolder.Meta, out var b)) { Console.Error.WriteLine("not found"); return 1; }
        int pbase = 0x10;
        int dataOff = BitConverter.ToInt32(b, pbase + 80);
        int dataSize = BitConverter.ToInt32(b, pbase + 84);
        int count = dataSize / stride;
        int start = pbase + dataOff;
        Console.WriteLine($"entries={count} stride={stride} start=0x{start:X} (dataOff={dataOff} dataSize={dataSize})");
        for (int i = 0; i < count; i++)
        {
            int e = start + i * stride;
            if (e + stride > b.Length) break;
            int n = 0; while (n < 256 && b[e + n] != 0) n++;
            string name = System.Text.Encoding.ASCII.GetString(b, e, n);
            if (filt.Length > 0 && !name.Contains(filt, StringComparison.OrdinalIgnoreCase)) continue;
            uint gbid = BitConverter.ToUInt32(b, e + 256);
            if (filt.Length == 0)
            {
                Console.WriteLine($"[{i,3}] gbid=0x{gbid:X8}  {name}");
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                for (int o = 256; o + 4 <= stride; o += 4)
                    sb.Append($"+{o}={BitConverter.ToInt32(b, e + o)} ");
                Console.WriteLine($"[idx {i,3}] {name}  gbid=0x{gbid:X8}");
                Console.WriteLine($"    {sb}");
            }
        }
        return 0;
    }
    case "powersf":
    {
        // Recon: dump a Power's decoded ScriptFormulas + ResolvedFormulas.
        // powersf <sno...>
        if (argv.Count < 2) { Console.Error.WriteLine("powersf <sno...>"); return 2; }
        foreach (var s in argv.Skip(1))
        {
            int id = int.Parse(s);
            var pow = d4.ReadPower(id);
            Console.WriteLine($"{id}  {pow.Name}  slots={pow.ScriptFormulas.Count}");
            foreach (var f in pow.ScriptFormulas)
                Console.WriteLine($"    SF_{f.Index}  text=\"{f.Text}\"  lit={f.LiteralValue}  expr={f.IsExpression}");
            var keys = pow.ResolvedFormulas.Keys.OrderBy(k => k, StringComparer.Ordinal);
            Console.WriteLine("    resolved: " + string.Join(", ", keys.Select(k => $"{k}={pow.ResolvedFormulas[k]}")));
            // Raw tail hex (last 128 bytes) — 16-byte rows, hex + ASCII, so
            // the slot records / ("0",0) terminator are inspectable.
            if (d4.TryReadSno((int)SnoGroup.Power, id, SnoFolder.Meta, out var blob))
            {
                int start = Math.Max(0, blob.Length - 128);
                for (int r = start; r < blob.Length; r += 16)
                {
                    int n = Math.Min(16, blob.Length - r);
                    var hex = new System.Text.StringBuilder();
                    var asc = new System.Text.StringBuilder();
                    for (int k = 0; k < n; k++)
                    {
                        hex.Append(blob[r + k].ToString("X2")).Append(' ');
                        byte bb = blob[r + k];
                        asc.Append(bb >= 0x20 && bb < 0x7F ? (char)bb : '.');
                    }
                    Console.WriteLine($"    @{r,5}  {hex,-48} {asc}");
                }
            }
        }
        return 0;
    }
    case "affixdump":
    {
        // LIB-3 recon: dump a g104 affix's struct fields + every VLA (8-byte
        // descriptor dataOff@+0/size@+4) with its contents, to map the effect
        // layout. affixdump <sno> [structbytes=0x100]
        if (argv.Count < 2) { Console.Error.WriteLine("affixdump <sno> [structbytes]"); return 2; }
        int asno = int.Parse(argv[1]);
        int structBytes = argv.Count > 2 ? Convert.ToInt32(argv[2], 16) : 0x100;
        if (!d4.TryReadSno(104, asno, SnoFolder.Meta, out var b)) { Console.WriteLine("no content"); return 1; }
        var r = new SnoRecord(b);
        int len = b.Length, pb = SnoRecord.DefaultPayloadBase;
        Console.WriteLine($"affix {asno} len={len}");
        Console.WriteLine("-- struct fields (payloadOff: int | float) --");
        for (int o = 0; o < structBytes && pb + o + 4 <= len; o += 4)
        {
            uint u = r.U32(o); float f = r.F32(o);
            string fs = (u != 0 && Math.Abs(f) is > 1e-3f and < 1e7f) ? $" f={f:0.###}" : "";
            string vla = "";
            // 8-byte VLA descriptor heuristic: dataOff in data region, size>0, fits.
            if (o + 8 <= structBytes) { int d0 = (int)u, d1 = (int)r.U32(o + 4);
                if (d0 >= structBytes && d0 < len - pb && d1 > 0 && d1 % 4 == 0 && pb + d0 + d1 <= len)
                    vla = $"  <VLA dataOff=0x{d0:X} size={d1}>"; }
            if (u != 0 || vla.Length > 0)
                Console.WriteLine($"  +0x{o:X2}: {(int)u,11} (0x{u:X8}){fs}{vla}");
        }
        // Dump each detected VLA's contents.
        Console.WriteLine("-- VLA contents --");
        for (int o = 0; o + 8 <= structBytes && pb + o + 8 <= len; o += 4)
        {
            int d0 = r.I32(o), d1 = r.I32(o + 4);
            if (!(d0 >= structBytes && d0 < len - pb && d1 > 0 && d1 % 4 == 0 && pb + d0 + d1 <= len)) continue;
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < d1 / 4 && k < 32; k++)
            {
                uint u = r.U32(d0 + k * 4); float f = r.F32(d0 + k * 4);
                string fs = (u != 0 && Math.Abs(f) is > 1e-3f and < 1e7f) ? $"={f:0.##}f" : "";
                sb.Append($"{(int)u}{fs} ");
            }
            Console.WriteLine($"  desc@+0x{o:X2} -> dataOff=0x{d0:X} size={d1}: [{sb.ToString().Trim()}]");
        }
        return 0;
    }
    case "inlineformula":
    {
        // LIB-3 R5: does the affix record carry an INLINE formula string
        // (DT_STRING_FORMULA) for the roll, when idx16 is NoGbid? Scan every
        // matching g104 affix for a printable ASCII run that looks like a
        // formula (contains '(' and a digit), report it, and measure coverage
        // over affixes whose Desc carries a rollable [Affix_Value_N].
        //   inlineformula <substr|ALL> [max=8000]
        string sub = argv.Count > 1 && argv[1] != "ALL" ? argv[1] : "";
        int max = argv.Count > 2 ? int.Parse(argv[2]) : 8000;
        int pb = SnoRecord.DefaultPayloadBase;
        int withValueToken = 0, withInline = 0, printed = 0, bothCount = 0;
        foreach (var e in toc.Entries.Where(e => (int)e.Group == 104
                     && e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase)).Take(max))
        {
            if (!d4.TryReadSno(104, e.Id, SnoFolder.Meta, out var b)) continue;
            string desc = ""; try { desc = d4.ReadAffix(e.Id).Description; } catch { }
            bool hasValueToken = desc.Contains("[Affix_Value", StringComparison.Ordinal);
            if (hasValueToken) withValueToken++;
            // Principled: read the +0xB0 modifier array; for each modifier with
            // idx16 == NoGbid, idx10 = inline formula string offset, idx11 = len.
            var r = new SnoRecord(b); int len = b.Length;
            string best = "";
            if (pb + 0xB0 + 8 <= len)
            {
                int mOff = r.I32(0xB0), mSize = r.I32(0xB4);
                if (mOff > 0 && mSize > 0 && mSize % 104 == 0 && pb + mOff + mSize <= len)
                {
                    for (int m = 0; m < mSize / 104; m++)
                    {
                        int mb = mOff + m * 104;
                        uint gbid = r.U32(mb + 64);            // idx16
                        if (gbid != 0xFFFFFFFF) continue;      // GBID-referenced, not inline
                        int sOff = r.I32(mb + 40), sLen = r.I32(mb + 44);   // idx10, idx11
                        if (sOff <= 0 || sLen <= 2 || sLen > 512 || pb + sOff + sLen > len) continue;
                        // validate printable
                        bool ok = true; for (int k = 0; k < sLen - 1 && ok; k++) if (b[pb + sOff + k] is < 0x20 or >= 0x7f) ok = false;
                        if (!ok) continue;
                        var s = System.Text.Encoding.ASCII.GetString(b, pb + sOff, sLen).TrimEnd('\0');
                        if (s.Length > best.Length) best = s;
                    }
                }
            }
            if (best.Length > 0)
            {
                withInline++;
                if (hasValueToken && printed < 30 && sub.Length > 0)
                { Console.WriteLine($"{e.Id} {e.Name}: {best}"); printed++; }
            }
            if (hasValueToken && best.Length > 0) bothCount++;
        }
        Console.WriteLine($"-- {withValueToken} carry a rollable [Affix_Value_N] desc; {withInline} carry an inline formula (idx16=NoGbid → idx10/11); intersection (value-token AND inline) = {bothCount} --");
        return 0;
    }
    case "inlinedump":
    {
        // R7 recon: dump EVERY g104 affix inline formula (idx16=NoGbid →
        // idx10 offset / idx11 len string), one line per modifier, as
        // id<TAB>name<TAB>formula. Unfiltered + uncapped so the corpus can be
        // grepped/counted offline (CurrentLegendaryRank, PowerTag."Script
        // Formula N", ternary/comparison, residual characterisation).
        //   inlinedump [substr]
        string sub = argv.Count > 1 ? argv[1] : "";
        int pb = SnoRecord.DefaultPayloadBase;
        int rows = 0, affixesWithInline = 0;
        foreach (var e in toc.Entries.Where(e => (int)e.Group == 104
                     && e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase)))
        {
            if (!d4.TryReadSno(104, e.Id, SnoFolder.Meta, out var b)) continue;
            var r = new SnoRecord(b); int len = b.Length;
            if (pb + 0xB0 + 8 > len) continue;
            int mOff = r.I32(0xB0), mSize = r.I32(0xB4);
            if (!(mOff > 0 && mSize > 0 && mSize % 104 == 0 && pb + mOff + mSize <= len)) continue;
            bool any = false;
            for (int m = 0; m < mSize / 104; m++)
            {
                int mb = mOff + m * 104;
                uint gbid = r.U32(mb + 64);                 // idx16
                if (gbid != 0xFFFFFFFF) continue;            // GBID-referenced, not inline
                int sOff = r.I32(mb + 40), sLen = r.I32(mb + 44);   // idx10, idx11
                if (sOff <= 0 || sLen <= 2 || sLen > 512 || pb + sOff + sLen > len) continue;
                bool ok = true; for (int k = 0; k < sLen - 1 && ok; k++) if (b[pb + sOff + k] is < 0x20 or >= 0x7f) ok = false;
                if (!ok) continue;
                var s = System.Text.Encoding.ASCII.GetString(b, pb + sOff, sLen).TrimEnd('\0');
                if (s.Length == 0) continue;
                Console.WriteLine($"{e.Id}\t{e.Name}\t{s}");
                rows++; any = true;
            }
            if (any) affixesWithInline++;
        }
        Console.Error.WriteLine($"-- {rows} inline-formula modifier rows across {affixesWithInline} affixes --");
        return 0;
    }
    case "ranksentinel":
    {
        // R7 verification: is the ("10",10.0) max-rank sentinel UNIVERSAL across
        // legendary powers, or per-power? For every g29 power matching <substr>
        // (default "legendary_"), scan the blob tail for the sentinel signature
        // — a 2-digit ASCII run + type tag 0x06 + its IEEE-754 float, taken as
        // the LAST such record before the ("0",0.0) terminator. Tally the
        // distinct sentinel values; dump any that are NOT 10.0.
        //   ranksentinel [substr]
        string sub = argv.Count > 1 ? argv[1] : "legendary_";
        int pb = SnoRecord.DefaultPayloadBase;
        var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int scanned = 0, noSentinel = 0, nonTen = 0;
        foreach (var e in toc.Entries.Where(e => (int)e.Group == 29
                     && e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase)))
        {
            if (!d4.TryReadSno(29, e.Id, SnoFolder.Meta, out var b)) continue;
            scanned++;
            // Collect, in order, every (digit-ASCII, tag 0x06, float) record:
            // a 1..3 digit run at a 4-byte cell, tag 0x06 in the next cell, float
            // 8 bytes past the run start. The tail is [...slots...]("10",10.0)("0",0.0);
            // the max-rank sentinel is the record BEFORE the ("0",0.0) terminator.
            var recs = new List<(string A, float F)>();
            for (int p = pb; p + 12 <= b.Length; p++)
            {
                if (b[p] is < (byte)'0' or > (byte)'9') continue;
                int q = p; while (q < b.Length && b[q] is >= (byte)'0' and <= (byte)'9') q++;
                int runLen = q - p;
                if (runLen is < 1 or > 3) continue;
                int tagOff = p + 4;
                if (tagOff + 8 > b.Length) continue;
                if (!(b[tagOff] == 0x06 && b[tagOff + 1] == 0 && b[tagOff + 2] == 0 && b[tagOff + 3] == 0)) continue;
                recs.Add((System.Text.Encoding.ASCII.GetString(b, p, runLen), BitConverter.ToSingle(b, tagOff + 4)));
                p = tagOff + 7;   // skip past this record
            }
            // Sentinel = pre-terminator record. If the last record is the
            // ("0",0.0) terminator, take the one before it; else take the last.
            (string A, float F)? sentinel = recs.Count switch
            {
                0 => null,
                _ when recs[^1].A == "0" && Math.Abs(recs[^1].F) < 1e-6f && recs.Count >= 2 => recs[^2],
                _ => recs[^1],
            };
            if (sentinel is null) { noSentinel++; continue; }
            string key = $"{sentinel.Value.A}={sentinel.Value.F:0.###}";
            tally.TryGetValue(key, out int c); tally[key] = c + 1;
            if (!(Math.Abs(sentinel.Value.F - 10f) < 0.001f))
            {
                nonTen++;
                if (nonTen <= 25) Console.WriteLine($"  NON-10: {e.Id} {e.Name}  sentinel {key}");
            }
        }
        Console.WriteLine($"-- scanned {scanned} g29 '{sub}' powers; {noSentinel} had no trailing digit/float record; {nonTen} had a non-10 last sentinel --");
        foreach (var kv in tally) Console.WriteLine($"   {kv.Value,5}  last-record {kv.Key}");
        return 0;
    }
    case "rollableresidual":
    {
        // R7 item 4: characterise the residual — g104 affixes whose localized
        // Desc carries a rollable [Affix_Value_N] but NO modifier has an inline
        // formula. For each, report whether ANY modifier still has a real
        // FormulaGbid (=> computable via the AttributeFormulas GBID path) vs
        // neither (truly un-printable). Tallies the honest split.
        //   rollableresidual [max=8000]
        int pb = SnoRecord.DefaultPayloadBase;
        int rollable = 0, noInline = 0, noInlineButGbid = 0, noInlineNoGbid = 0, listed = 0;
        foreach (var e in toc.Entries.Where(e => (int)e.Group == 104))
        {
            if (!d4.TryReadSno(104, e.Id, SnoFolder.Meta, out var b)) continue;
            string desc = ""; try { desc = d4.ReadAffix(e.Id).Description; } catch { }
            if (!desc.Contains("[Affix_Value", StringComparison.Ordinal)) continue;
            rollable++;
            var r = new SnoRecord(b); int len = b.Length;
            if (pb + 0xB0 + 8 > len) continue;
            int mOff = r.I32(0xB0), mSize = r.I32(0xB4);
            if (!(mOff > 0 && mSize > 0 && mSize % 104 == 0 && pb + mOff + mSize <= len)) continue;
            bool hasInline = false, hasGbid = false;
            for (int m = 0; m < mSize / 104; m++)
            {
                int mb = mOff + m * 104;
                uint gbid = r.U32(mb + 64);
                if (gbid != 0xFFFFFFFF) { hasGbid = true; continue; }
                int sOff = r.I32(mb + 40), sLen = r.I32(mb + 44);
                if (sOff <= 0 || sLen <= 2 || sLen > 512 || pb + sOff + sLen > len) continue;
                bool ok = true; for (int k = 0; k < sLen - 1 && ok; k++) if (b[pb + sOff + k] is < 0x20 or >= 0x7f) ok = false;
                if (ok) hasInline = true;
            }
            if (hasInline) continue;
            noInline++;
            if (hasGbid) noInlineButGbid++; else noInlineNoGbid++;
            if (listed++ < 40)
                Console.WriteLine($"  {e.Id} {e.Name}  gbidFormula={(hasGbid ? "yes" : "NO")}  desc=\"{desc.Replace("\r"," ").Replace("\n"," ").Trim()}\"");
        }
        Console.WriteLine($"-- rollable [Affix_Value] Desc={rollable}; of these NO inline formula={noInline} (GBID-formula backed={noInlineButGbid}, neither inline nor GBID={noInlineNoGbid}) --");
        return 0;
    }
    case "affixstr":
    {
        // R7 recon: dump every printable ASCII run (>=2 chars) in a g104 affix
        // payload with its payload offset, so the modifier's string slots
        // (idx10 inline formula, idx14 companion, value tokens) are all visible.
        //   affixstr <sno...>
        if (argv.Count < 2) { Console.Error.WriteLine("affixstr <sno...>"); return 2; }
        int pb = SnoRecord.DefaultPayloadBase;
        foreach (var s in argv.Skip(1))
        {
            int id = int.Parse(s);
            if (!d4.TryReadSno(104, id, SnoFolder.Meta, out var b)) { Console.WriteLine($"{id}: no content"); continue; }
            Console.WriteLine($"## affix {id} len={b.Length}");
            int i = pb;
            while (i < b.Length)
            {
                if (b[i] is >= 0x20 and < 0x7f)
                {
                    int start = i;
                    while (i < b.Length && b[i] is >= 0x20 and < 0x7f) i++;
                    int n = i - start;
                    if (n >= 2)
                        Console.WriteLine($"   payload+{start - pb,-4} len={n,-3} \"{System.Text.Encoding.ASCII.GetString(b, start, n)}\"");
                }
                else i++;
            }
        }
        return 0;
    }
    case "affixcorpus":
    {
        // LIB-3 corpus: for each g104 affix matching <substr> (ALL = every),
        // emit a compact machine-readable block: struct fields (int|float),
        // every VLA (idx:int|float|hex), and the localized Name/Desc. This is
        // the RE corpus for cracking the item-affix effect layout.
        //   affixcorpus <substr|ALL> [max=80] [structbytes=0x100]
        if (argv.Count < 2) { Console.Error.WriteLine("affixcorpus <substr|ALL> [max] [structbytes]"); return 2; }
        string sub = argv[1] == "ALL" ? "" : argv[1];
        int max = argv.Count > 2 ? int.Parse(argv[2]) : 80;
        int structBytes = argv.Count > 3 ? Convert.ToInt32(argv[3], 16) : 0x100;
        int pb = SnoRecord.DefaultPayloadBase;
        var picks = toc.Entries.Where(e => (int)e.Group == 104
                        && e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.Name).Take(max).ToList();
        Console.WriteLine($"# affixcorpus substr='{sub}' count={picks.Count}");
        foreach (var e in picks)
        {
            if (!d4.TryReadSno(104, e.Id, SnoFolder.Meta, out var b)) continue;
            var r = new SnoRecord(b);
            int len = b.Length;
            string name = "", desc = "";
            try { var a = d4.ReadAffix(e.Id); name = a.Name; desc = a.Description; } catch { }
            Console.WriteLine($"## {e.Id} {e.Name} len={len}");
            if (name.Length > 0) Console.WriteLine($"   NAME: {name}");
            if (desc.Length > 0) Console.WriteLine($"   DESC: {desc.Replace("\r", " ").Replace("\n", " ")}");
            // Every plausible non-int float (min/max magnitude candidates), with payload offset.
            var flb = new System.Text.StringBuilder();
            for (int p = 0; pb + p + 4 <= len; p += 4)
            {
                float f = BitConverter.ToSingle(b, pb + p); uint u = BitConverter.ToUInt32(b, pb + p);
                bool intLike = u < 100000 && f == (int)f;
                if (!float.IsNaN(f) && !float.IsInfinity(f) && Math.Abs(f) is > 0.0001f and < 1e6f && !intLike)
                    flb.Append($" +{p:X2}={f:0.####}");
            }
            if (flb.Length > 0) Console.WriteLine($"   F:{flb}");
            var fsb = new System.Text.StringBuilder();
            for (int o = 0; o < structBytes && pb + o + 4 <= len; o += 4)
            {
                uint u = r.U32(o); float f = r.F32(o);
                if (u == 0) continue;
                string fs = (Math.Abs(f) is > 1e-3f and < 1e7f) ? $"|{f:0.###}" : (u >= 0x10000 ? $"|x{u:X}" : "");
                fsb.Append($" +{o:X2}={(int)u}{fs}");
            }
            Console.WriteLine($"   S:{fsb}");
            for (int o = 0; o + 8 <= structBytes && pb + o + 8 <= len; o += 4)
            {
                int d0 = r.I32(o), d1 = r.I32(o + 4);
                if (!(d0 >= structBytes && d0 < len - pb && d1 > 0 && d1 % 4 == 0 && pb + d0 + d1 <= len)) continue;
                var vb = new System.Text.StringBuilder();
                for (int k = 0; k < d1 / 4 && k < 60; k++)
                {
                    uint u = r.U32(d0 + k * 4); float f = r.F32(d0 + k * 4);
                    string fv = u == 0 ? "" : (Math.Abs(f) is > 1e-3f and < 1e7f) ? $"|{f:0.##}" : (u >= 0x10000 ? $"|x{u:X}" : "");
                    vb.Append($"{k}:{(int)u}{fv} ");
                }
                Console.WriteLine($"   V@+{o:X2}[off={d0:X},sz={d1}]: {vb.ToString().Trim()}");
            }
        }
        return 0;
    }
    case "affixeffects":
    {
        // LIB-3: dogfood the shipped AffixDefinition.Effects API on live data.
        //   affixeffects <sno...>
        if (argv.Count < 2) { Console.Error.WriteLine("affixeffects <sno...>"); return 2; }
        foreach (var s in argv.Skip(1))
        {
            int id = int.Parse(s);
            AffixDefinition a;
            try { a = d4.ReadAffix(id); } catch (Exception ex) { Console.WriteLine($"{id}: {ex.Message}"); continue; }
            var gbf = d4.ReadAttributeFormulas();
            string nm = a.Name.Length > 0 ? a.Name : "(no name)";
            Console.WriteLine($"{id} {nm}  [{a.Effects.Count} effect(s)]");
            if (a.Description.Length > 0) Console.WriteLine($"    desc: {a.Description.Replace("\n", " ")}");
            foreach (var e in a.Effects)
            {
                string pn = e.HasParam ? $" param=0x{e.ParamPlus12:X8}" : "";
                string an = e.AttributeName.Length > 0 ? e.AttributeName : "(unresolved)";
                string fg = e.FormulaGbid != AffixEffect.NoFormula
                    ? (gbf.TryGetByGbid(e.FormulaGbid, out var fm)
                        ? $"  formula={fm.Name} \"{fm.PrimaryText}\""
                        : $"  formula=0x{e.FormulaGbid:X8}(unresolved)")
                    : (e.InlineFormula.Length > 0 ? $"  inline=\"{e.InlineFormula}\"" : "");
                Console.WriteLine($"    attr {e.AttributeId,5}{pn} -> {an}{fg}");
            }
            if (a.StaticValues.Count > 0)
                Console.WriteLine($"    StaticValues: [{string.Join(", ", a.StaticValues)}]");
        }
        return 0;
    }
    case "affixattrmap":
    {
        // LIB-3 validation + FR-C27 bonus: for every SINGLE-modifier g104 affix
        // (+0xB0 VLA size==104) that carries a Desc placeholder [Token...], pair
        // its modifier AttributeId (idx4) with the leading Desc value-token, and
        // aggregate idx4 -> {tokens}. A clean 1:1 map validates "idx4 = the
        // modified attribute" AND yields an AttributeId->name registry.
        //   affixattrmap [minlen=3]
        int pb = SnoRecord.DefaultPayloadBase;
        var map = new SortedDictionary<int, Dictionary<string, int>>();  // idx4 -> token -> count
        var paramMap = new SortedDictionary<int, HashSet<int>>();        // idx4 -> {idx7 params}
        int scanned = 0, withDesc = 0, single = 0;
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 104) continue;
            if (!d4.TryReadSno(104, e.Id, SnoFolder.Meta, out var b)) continue;
            var r = new SnoRecord(b); scanned++;
            // locate the +0xB0 effect VLA
            int descOff = 0xB0;
            if (pb + descOff + 8 > b.Length) continue;
            int dataOff = r.I32(descOff), size = r.I32(descOff + 4);
            if (dataOff <= 0 || size <= 0 || size % 104 != 0 || pb + dataOff + size > b.Length) continue;
            if (size != 104) continue;   // single modifier only, for a clean correlation
            single++;
            int attrId = r.I32(dataOff + 16);   // idx4
            int param = r.I32(dataOff + 28);    // idx7
            if (!paramMap.TryGetValue(attrId, out var ps)) { ps = new(); paramMap[attrId] = ps; }
            ps.Add(param);
            string desc = "";
            try { desc = d4.ReadAffix(e.Id).Description; } catch { }
            if (desc.Length == 0) continue;
            int lb = desc.IndexOf('[');
            if (lb < 0) continue;
            // leading identifier token inside the first bracket
            int j = lb + 1; while (j < desc.Length && (desc[j] == ' ')) j++;
            int st = j; while (j < desc.Length && (char.IsLetterOrDigit(desc[j]) || desc[j] == '_')) j++;
            string tok = desc.Substring(st, j - st);
            if (tok.Length < 3 || char.IsDigit(tok[0])) continue;
            withDesc++;
            if (!map.TryGetValue(attrId, out var d)) { d = new(); map[attrId] = d; }
            d[tok] = d.GetValueOrDefault(tok) + 1;
        }
        Console.WriteLine($"# affixattrmap: {scanned} g104, {single} single-modifier, {withDesc} with Desc token");
        int conflicts = 0;
        Console.WriteLine("# AttributeId -> token(s) [count]   (params seen)");
        foreach (var kv in map)
        {
            var toks = kv.Value.OrderByDescending(t => t.Value).ToList();
            if (toks.Count > 1) conflicts++;
            string ts = string.Join(" | ", toks.Select(t => $"{t.Key}[{t.Value}]"));
            string ps = paramMap.TryGetValue(kv.Key, out var s) ? string.Join(",", s.OrderBy(x => x)) : "";
            Console.WriteLine($"  {kv.Key,4} -> {ts}   (p:{ps})");
        }
        Console.WriteLine($"# {map.Count} distinct AttributeIds mapped; {conflicts} with >1 token (conflicts)");
        return 0;
    }
    case "affixfloatscan":
    {
        // LIB-3: across EVERY g104 affix, find every plausible float
        // (0.0001<|f|<1e6, excluding tiny-int aliases) and aggregate by
        // payload offset — a min/max value field shows as an offset present
        // in most affixes with widely-varying values. Reports per-offset
        // coverage + sample values, and per-affix max float count.
        //   affixfloatscan [maxsamples=8]
        int maxSamples = argv.Count > 1 ? int.Parse(argv[1]) : 8;
        int pb = SnoRecord.DefaultPayloadBase;
        var byOff = new SortedDictionary<int, List<float>>();
        int scanned = 0, maxFloatsInOne = 0; string maxFloatsSno = "";
        var histFloatCount = new SortedDictionary<int, int>();
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 104) continue;
            if (!d4.TryReadSno(104, e.Id, SnoFolder.Meta, out var b)) continue;
            scanned++;
            int inThis = 0;
            for (int p = 0; pb + p + 4 <= b.Length; p += 4)
            {
                float f = BitConverter.ToSingle(b, pb + p);
                uint u = BitConverter.ToUInt32(b, pb + p);
                // plausible non-integer magnitude (exclude small ints / exact 1.0 which is the known flag)
                bool intLike = u < 100000 && f == (int)f;
                if (float.IsNaN(f) || float.IsInfinity(f)) continue;
                if (Math.Abs(f) is > 0.0001f and < 1e6f && !intLike)
                {
                    if (!byOff.TryGetValue(p, out var l)) { l = new(); byOff[p] = l; }
                    l.Add(f); inThis++;
                }
            }
            histFloatCount[inThis] = histFloatCount.GetValueOrDefault(inThis) + 1;
            if (inThis > maxFloatsInOne) { maxFloatsInOne = inThis; maxFloatsSno = e.Name; }
        }
        Console.WriteLine($"# affixfloatscan over {scanned} g104 affixes");
        Console.WriteLine($"# per-affix non-int-float count histogram (count -> #affixes):");
        foreach (var kv in histFloatCount) Console.WriteLine($"   {kv.Key} floats -> {kv.Value} affixes");
        Console.WriteLine($"# max floats in one affix: {maxFloatsInOne} ({maxFloatsSno})");
        Console.WriteLine($"# per-offset coverage (offset: nAffixes | distinct-sample-values):");
        foreach (var kv in byOff.OrderByDescending(k => k.Value.Count))
        {
            var distinct = kv.Value.Distinct().OrderBy(x => x).ToList();
            var sample = string.Join(",", distinct.Take(maxSamples).Select(x => x.ToString("0.####")));
            Console.WriteLine($"   +0x{kv.Key:X2}: {kv.Value.Count,5} affixes | {distinct.Count} distinct | [{sample}{(distinct.Count > maxSamples ? ",…" : "")}]");
        }
        return 0;
    }
    case "locate":
    {
        // LIB-2: exercise the install auto-detector.
        bool ok = Diablo4Storage.TryLocateInstall(out var path);
        Console.WriteLine($"TryLocateInstall -> {ok}  path='{path}'");
        if (ok)
        {
            using var located = Diablo4Storage.Open();   // no-arg auto-detect
            Console.WriteLine($"Open() auto-detected; CoreTOC entries = {located.CoreToc.Entries.Count:N0}");
        }
        return ok ? 0 : 1;
    }
    case "itemtypes":
    {
        // LIB-1 recon: dump every g98 ItemType's candidate category/slot fields
        // to find the taxonomy. Prints id, several early int32s, name.
        var byCat = new SortedDictionary<int, List<string>>();
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 98) continue;
            if (!d4.TryReadSno(98, e.Id, SnoFolder.Meta, out var b)) continue;
            var r = new SnoRecord(b);
            int f8 = b.Length >= 0x1C ? r.I32(8) : -1;    // candidate category enum
            int f44 = b.Length >= 0x58 ? r.I32(0x44) : -1;
            int f48 = b.Length >= 0x5C ? r.I32(0x48) : -1;
            if (!byCat.TryGetValue(f8, out var l)) { l = new(); byCat[f8] = l; }
            l.Add($"{e.Name}(len={b.Length},f44={f44},f48={f48})");
        }
        foreach (var kv in byCat)
            Console.WriteLine($"cat@+8={kv.Key,3}: [{kv.Value.Count}] {string.Join(", ", kv.Value.Take(24))}");
        // gear-only discriminators: for cat in {32,48}, print @+8, @+0xC, @+0x30(weaponSub), @+0x44
        Console.WriteLine("\n== gear types (cat 32/48): name | c8 | cC | w30 | f44 ==");
        var rows = new List<(int c8, int cc, int w30, int f44, string name)>();
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 98) continue;
            if (!d4.TryReadSno(98, e.Id, SnoFolder.Meta, out var b)) continue;
            var r = new SnoRecord(b);
            if (b.Length < 0x60) continue;
            int c8 = r.I32(8);
            if (c8 is not (32 or 48)) continue;
            rows.Add((c8, r.I32(0xC), r.I32(0x30), r.I32(0x44), e.Name));
        }
        foreach (var t in rows.OrderBy(t => t.c8).ThenBy(t => t.w30 < 0 ? 1 : 0).ThenBy(t => t.cc).ThenBy(t => t.name))
            Console.WriteLine($"  {t.name,-20} c8={t.c8,2} cC={t.cc} w30={t.w30,3} f44={t.f44,2}");
        Console.WriteLine("\n== API classification (Diablo4Storage.EnumerateItemTypes) ==");
        foreach (var g in d4.EnumerateItemTypes().GroupBy(t => t.Class).OrderBy(g => g.Key))
            Console.WriteLine($"{g.Key,-8}: [{g.Count()}] {string.Join(", ", g.Select(t => t.Name).OrderBy(n => n).Take(40))}");
        return 0;
    }
    case "classstats":
    {
        // FR-C29 validation: decode ReadPlayerClass for every real class and
        // print the per-class core->bonus map + full conversion table.
        foreach (var e in toc.Entries)
        {
            if ((int)e.Group != 74) continue;
            PlayerClassDefinition pc; try { pc = d4.ReadPlayerClass(e.Id); } catch { continue; }
            if (pc.PrimaryAttribute is null) { Console.WriteLine($"{e.Name,-12} (sno {e.Id}) — no map (placeholder)"); continue; }
            Console.WriteLine($"{e.Name,-12} (sno {e.Id}, eClass {pc.EClass}): SkillDmg<-{pc.PrimaryAttribute}  Crit<-{pc.CriticalStrikeAttribute}  ResGen<-{pc.ResourceGenerationAttribute}");
            foreach (var cv in pc.StatConversions)
                Console.WriteLine($"     {cv.Core,-12} -> {cv.Stat,-22} {cv.PerPoint}{(cv.Unit == ConversionUnit.Percent ? "%" : "")}/pt");
        }
        return 0;
    }
    case "f32grep":
    {
        // FR-C29: scan a group's Meta records for an IEEE-754 float within a
        // relative tolerance; report SNO + offset + neighbouring floats (so a
        // coefficient adjacent to a name-hash key surfaces). Reads whole group.
        //   f32grep <value> [tolpct=0.5] [gid=20] [maxhits=200]
        if (argv.Count < 2) { Console.Error.WriteLine("f32grep <value> [tolpct=0.5] [gid=20] [maxhits=200]"); return 2; }
        double want = double.Parse(argv[1]);
        double tol = (argv.Count > 2 ? double.Parse(argv[2]) : 0.5) / 100.0;
        int fg = argv.Count > 3 ? int.Parse(argv[3]) : 20;
        int fmax = argv.Count > 4 ? int.Parse(argv[4]) : 200;
        double lo = want - Math.Abs(want) * tol - 1e-12, hi = want + Math.Abs(want) * tol + 1e-12;
        int hits = 0, scanned = 0;
        foreach (var e in toc.Entries)
        {
            if (fg >= 0 && (int)e.Group != fg) continue;
            if (!d4.TryReadSno((int)e.Group, e.Id, SnoFolder.Meta, out var b)) continue;
            scanned++;
            for (int i = 0; i + 4 <= b.Length; i += 4)
            {
                float f = BitConverter.ToSingle(b, i);
                if (!(f >= lo && f <= hi)) continue;
                var sb = new System.Text.StringBuilder();
                for (int j = -8; j <= 12; j += 4)
                {
                    int o = i + j; if (o < 0 || o + 4 > b.Length) { sb.Append("        .   "); continue; }
                    uint u = BitConverter.ToUInt32(b, o); float g = BitConverter.ToSingle(b, o);
                    string gv = (u != 0 && Math.Abs(g) is > 1e-5f and < 1e8f) ? $"={g:0.####}" : "";
                    sb.Append(j == 0 ? "[" : " ").Append($"{u:X8}{gv}").Append(j == 0 ? "]" : "");
                }
                Console.WriteLine($"g{(int)e.Group} {e.Id,9} @0x{i:X4} {sb}  {e.Name}");
                if (++hits >= fmax) { Console.WriteLine("-- maxhits --"); goto donef; }
            }
        }
        donef:
        Console.WriteLine($"-- {hits} hit(s) for {want} (±{tol*100}%) over {scanned} SNO(s) in group {fg} --");
        return 0;
    }
    case "hashgrep":
    {
        // FR-C29 name-hash-grep: hash candidate names (fieldHash/gbid/typeHash),
        // grep a group's Meta for any 32-bit-aligned match; cluster by SNO with
        // the adjacent float (a coefficient bound to a named engine field).
        //   hashgrep <gid> <name...>   (gid=-1 scans every group)
        if (argv.Count < 3) { Console.Error.WriteLine("hashgrep <gid> <name...>"); return 2; }
        int hg = int.Parse(argv[1]);
        var targets = new Dictionary<uint, string>();
        foreach (var nm in argv.Skip(2))
        {
            void T(uint h, string kind) { if (h > 0xFFFF) targets.TryAdd(h, $"{nm}[{kind}]"); }
            T(Diablo4.FieldHash(nm), "field"); T(Diablo4.TypeHash(nm), "type"); T(Diablo4.GbidHash(nm), "gbid");
        }
        Console.WriteLine($"{targets.Count} target hash(es) from {argv.Count - 2} name(s)");
        int hits = 0, scanned = 0;
        foreach (var e in toc.Entries)
        {
            if (hg >= 0 && (int)e.Group != hg) continue;
            if (!d4.TryReadSno((int)e.Group, e.Id, SnoFolder.Meta, out var b)) continue;
            scanned++;
            for (int i = 0; i + 4 <= b.Length; i += 4)
            {
                uint u = BitConverter.ToUInt32(b, i);
                if (!targets.TryGetValue(u, out var who)) continue;
                bool hasNext = i + 8 <= b.Length;
                float adj = hasNext ? BitConverter.ToSingle(b, i + 4) : 0;
                string av = Math.Abs(adj) is > 1e-5f and < 1e8f ? $"{adj:0.####}" : "";
                uint next = hasNext ? BitConverter.ToUInt32(b, i + 4) : 0;
                Console.WriteLine($"g{(int)e.Group} {e.Id,9} @0x{i:X4} = {who}  next=0x{next:X8} ({av})  {e.Name}");
                if (++hits >= 400) goto doneh;
            }
        }
        doneh:
        Console.WriteLine($"-- {hits} hit(s) over {scanned} SNO(s) in group {hg} --");
        return 0;
    }
    default:
        Console.Error.WriteLine($"unknown command '{cmd}'");
        return 2;
}

// D4 serialization hashes — DJB2 core (hash*33 + ch), SEED 0 (NOT 5381).
// typeHash: no-lowercase, full u32. fieldHash = typeHash & 0x0FFFFFFF.
// gbidHash: lowercased, full u32.
static class D4
{
    public static uint TypeHash(string s)
    {
        uint h = 0;
        foreach (char c in s) h = (h << 5) + h + (byte)c;
        return h;
    }
    public static uint GbidH(string s)
    {
        uint h = 0;
        foreach (char c in s.ToLowerInvariant()) h = (h << 5) + h + (byte)c;
        return h;
    }
}
