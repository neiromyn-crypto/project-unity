"""Read-only FBX binary inventory: embedded data and geometry array counts."""
import json, pathlib, struct, zlib, array
ROOT=pathlib.Path(__file__).resolve().parents[2]
def inspect(path):
    result={'file':str(path.relative_to(ROOT)),'bytes':path.stat().st_size,'embeddedBytes':0,'vertices':0,'polygonIndices':0,'triangles':0,'videos':0}
    with path.open('rb') as f:
        if f.read(23)!=b'Kaydara FBX Binary  \x00\x1a\x00':return result
        version=struct.unpack('<I',f.read(4))[0];wide=version>=7500;header='<QQQB' if wide else '<IIIB';size=25 if wide else 13
        def node():
            data=f.read(size)
            if len(data)!=size:return False
            end,count,length,nlen=struct.unpack(header,data)
            if end==0:return False
            name=f.read(nlen).decode('utf8',errors='replace')
            if name=='Video':result['videos']+=1
            for _ in range(count):
                typ=f.read(1)
                if typ in (b'S',b'R'):
                    n=struct.unpack('<I',f.read(4))[0]
                    if name=='Content':result['embeddedBytes']+=n
                    f.seek(n,1)
                elif typ in (b'f',b'd',b'l',b'i',b'b',b'c'):
                    n,encoding,compressed=struct.unpack('<III',f.read(12))
                    if name=='Vertices':result['vertices']+=n//3
                    if name=='PolygonVertexIndex':
                        result['polygonIndices']+=n
                        raw=f.read(compressed);raw=zlib.decompress(raw) if encoding else raw
                        indices=array.array('i');indices.frombytes(raw)
                        result['triangles']+=n-2*sum(1 for x in indices if x<0)
                    else:f.seek(compressed,1)
                else:
                    lengths={b'Y':2,b'C':1,b'I':4,b'F':4,b'D':8,b'L':8}
                    if typ not in lengths:raise ValueError((typ,name,path))
                    f.seek(lengths[typ],1)
            while f.tell()<end:
                if not node():break
            f.seek(end);return True
        while node():pass
    return result
rows=[inspect(p) for p in (ROOT/'Assets').rglob('*.fbx')]
rows.sort(key=lambda r:r['bytes'],reverse=True)
out=pathlib.Path(__file__).with_name('fbx-audit-2026-09-10.json');out.write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(rows[:22],ensure_ascii=False,indent=2))
