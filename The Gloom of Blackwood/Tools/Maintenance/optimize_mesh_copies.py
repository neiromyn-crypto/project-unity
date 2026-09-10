"""Technical simplification of existing Unity meshes; no new art is generated.
Run with Blender --background --factory-startup --python this_file.
UVs, material slots and skeletal weights are retained in the interchange.
"""
import bpy, numpy as np, pathlib, json, struct, time
ROOT=pathlib.Path(__file__).resolve().parents[2]
WORK=ROOT/'IntegrationEvidence/Optimization'
jobs=json.loads((WORK/'jobs.json').read_text(encoding='utf-8-sig'))['meshes']
report=[]
for job in jobs:
    start=time.time();name=job['id'];print('OPTIMIZING',job['name'],job['triangles'],'->',job['target'],flush=True)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    with (WORK/'Input'/f'{name}.bwm').open('rb') as stream:
        count,subs,skin=struct.unpack('<iii',stream.read(12))
        data=np.fromfile(stream,dtype='<f4',count=count*8).reshape(count,8)
        weights=np.fromfile(stream,dtype=np.dtype([('bones','<i4',(4,)),('weights','<f4',(4,))]),count=count) if skin else None
        tris=[];mats=[]
        for sub in range(subs):
            length=struct.unpack('<i',stream.read(4))[0];tri=np.fromfile(stream,dtype='<i4',count=length).reshape(-1,3);tris.append(tri);mats.extend([sub]*len(tri))
    triangles=np.concatenate(tris);mesh=bpy.data.meshes.new(name)
    mesh.vertices.add(count);mesh.vertices.foreach_set('co',data[:,:3].ravel())
    mesh.loops.add(triangles.size);mesh.loops.foreach_set('vertex_index',triangles.ravel())
    mesh.polygons.add(len(triangles));mesh.polygons.foreach_set('loop_start',np.arange(len(triangles),dtype=np.int32)*3);mesh.polygons.foreach_set('loop_total',np.full(len(triangles),3,dtype=np.int32));mesh.polygons.foreach_set('material_index',mats)
    uv=mesh.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',data[triangles.ravel(),6:8].ravel());mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    for sub in range(subs):obj.data.materials.append(bpy.data.materials.new('Slot'+str(sub)))
    if skin:
        maxbone=int(weights['bones'].max());groups=[obj.vertex_groups.new(name='Bone'+str(i)) for i in range(maxbone+1)]
        for i in range(count):
            for bone,value in zip(weights['bones'][i],weights['weights'][i]):
                if value>0:groups[int(bone)].add([i],float(value),'REPLACE')
    mod=obj.modifiers.new('Game geometry budget','DECIMATE');mod.ratio=min(1,job['target']/job['triangles']);mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    out=obj.data;out.calc_loop_triangles()
    # Split UV seams only; normals follow the original imported smooth/flat appearance.
    coords=[];normals=[];uvs=[];outweights=[];lookup={};outtris=[[] for _ in range(subs)]
    uv=out.uv_layers.active.data
    for face in out.loop_triangles:
        for vi,li in zip(face.vertices,face.loops):
            tex=uv[li].uv;normal=out.vertices[vi].normal
            key=(vi,round(tex.x,7),round(tex.y,7))
            if key not in lookup:
                lookup[key]=len(coords);coords.append(tuple(out.vertices[vi].co));normals.append(tuple(normal));uvs.append(tuple(tex))
                if skin:
                    items=sorted([(g.group,g.weight) for g in out.vertices[vi].groups if g.weight>0],key=lambda x:-x[1])[:4]
                    total=sum(w for _,w in items)
                    if total<=0:raise RuntimeError('Unweighted vertex after simplification')
                    items=[(b,w/total) for b,w in items]+[(0,0)]*(4-len(items));outweights.append(items)
            outtris[face.material_index].append(lookup[key])
    with (WORK/'Output'/f'{name}.bwm').open('wb') as stream:
        stream.write(struct.pack('<iii',len(coords),subs,skin));np.column_stack((coords,normals,uvs)).astype('<f4').tofile(stream)
        if skin:
            for items in outweights:stream.write(struct.pack('<4i4f',*[b for b,_ in items],*[w for _,w in items]))
        for indices in outtris:stream.write(struct.pack('<i',len(indices)));np.asarray(indices,dtype='<i4').tofile(stream)
    result={'id':name,'name':job['name'],'before':job['triangles'],'after':len(out.loop_triangles),'vertices':len(coords),'seconds':round(time.time()-start,2),'skinned':bool(skin)};report.append(result)
    (WORK/'reduction-report.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('DONE',result,flush=True)
print('ALL_OPTIMIZED',len(report),flush=True)
