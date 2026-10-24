import bpy
p=r'C:\Users\anagh\Documents\Morphic\Teapot 2 - shaft manual rotation with follow.blend'; bpy.ops.wm.open_mainfile(filepath=p); o=bpy.data.objects['Cylinder.002']; k=o.data.shape_keys.key_blocks; b=k['Basis']; lid=k['Lid 1']; h=k['Height']; parts={name:{i for i in range(len(b.data)) if (k[name].data[i].co-b.data[i].co).length>1e-5} for name in ['Lid 1','Lid 2','Shaft 1','Shaft 2','Handle 1','Handle 2']}; body=set(range(len(b.data)))-set().union(*parts.values())
for axis in range(3):
 bvals=[b.data[i].co[axis] for i in body]; hvals=[h.data[i].co[axis] for i in body]; ext=(max(hvals)-min(hvals))-(max(bvals)-min(bvals)); top=max(hvals)-max(bvals); print('AXIS',axis,'extent_delta',ext,'top_delta',top)
axis=max(range(3),key=lambda a:max(abs((max([h.data[i].co[a] for i in body])-min([h.data[i].co[a] for i in body]))-(max([b.data[i].co[a] for i in body])-min([b.data[i].co[a] for i in body]))),abs(max([h.data[i].co[a] for i in body])-max([b.data[i].co[a] for i in body]))))
lv=parts['Lid 1']; bmin=min(b.data[i].co[axis] for i in body); bmax=max(b.data[i].co[axis] for i in body); hmin=min(h.data[i].co[axis] for i in body); hmax=max(h.data[i].co[axis] for i in body)
print('LID1_AXIS',axis,'body range basis/height',bmin,bmax,hmin,hmax)
for label,key in [('Basis',b),('Height',h)]:
 vals=[key.data[i].co[axis] for i in lv]; print(label,'Lid1 min/max',min(vals),max(vals),'center',(min(vals)+max(vals))/2)
print('HEIGHT_LID_DELTA_AVG',sum(h.data[i].co[axis]-b.data[i].co[axis] for i in lv)/len(lv))
