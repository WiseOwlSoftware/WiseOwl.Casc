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
