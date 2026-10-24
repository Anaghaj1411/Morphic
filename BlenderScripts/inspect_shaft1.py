import bpy
from mathutils import Vector
p=r'C:\Users\anagh\Documents\Morphic\Teapot 2 - script fixed.blend'
bpy.ops.wm.open_mainfile(filepath=p)
o=bpy.data.objects['Cylinder.002']; keys=o.data.shape_keys.key_blocks
basis=keys['Basis']; k=keys['Shaft 1']
idx=[i for i,(a,b) in enumerate(zip(basis.data,k.data)) if (a.co-b.co).length>1e-5]
for label, points in [('basis', [basis.data[i].co for i in idx]),('shaft', [k.data[i].co for i in idx])]:
 print(label,'count',len(points),'bounds',[(min(v[a] for v in points),max(v[a] for v in points)) for a in range(3)])
 print(label,'centroid',sum(points,Vector())/len(points))
# nearest body surface using all non-shaft vertices
body=[basis.data[i].co for i in range(len(basis.data)) if i not in idx]
print('shaft nearest-body distances sorted',sorted(min((p-q).length for q in body) for p in [k.data[i].co for i in idx])[:20])
print('shape keys',[(x.name,x.value) for x in keys])
