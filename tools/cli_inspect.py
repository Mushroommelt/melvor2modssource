import struct,sys,os
from collections import defaultdict

CODED={
 'TypeDefOrRef':([2,1,27],2), 'HasConstant':([4,8,23],2),
 'HasCustomAttribute':([6,4,1,2,8,9,10,0,14,23,20,17,26,27,32,35,38,39,40,42,44,43],5),
 'HasFieldMarshal':([4,8],1), 'HasDeclSecurity':([2,6,32],2),
 'MemberRefParent':([2,1,26,6,27],3), 'HasSemantics':([20,23],1),
 'MethodDefOrRef':([6,10],1), 'MemberForwarded':([4,6],1),
 'Implementation':([38,35,39],2), 'CustomAttributeType':([None,None,6,10,None],3),
 'ResolutionScope':([0,26,35,1],2), 'TypeOrMethodDef':([2,6],1),
}
SCHEMA={
0:('Module',['u16','str','guid','guid','guid']),1:('TypeRef',['c:ResolutionScope','str','str']),
2:('TypeDef',['u32','str','str','c:TypeDefOrRef','i:4','i:6']),
3:('FieldPtr',['i:4']),4:('Field',['u16','str','blob']),5:('MethodPtr',['i:6']),
6:('MethodDef',['u32','u16','u16','str','blob','i:8']),7:('ParamPtr',['i:8']),
8:('Param',['u16','u16','str']),9:('InterfaceImpl',['i:2','c:TypeDefOrRef']),
10:('MemberRef',['c:MemberRefParent','str','blob']),
11:('Constant',['u8','u8','c:HasConstant','blob']),
12:('CustomAttribute',['c:HasCustomAttribute','c:CustomAttributeType','blob']),
13:('FieldMarshal',['c:HasFieldMarshal','blob']),14:('DeclSecurity',['u16','c:HasDeclSecurity','blob']),
15:('ClassLayout',['u16','u32','i:2']),16:('FieldLayout',['u32','i:4']),
17:('StandAloneSig',['blob']),18:('EventMap',['i:2','i:20']),
19:('EventPtr',['i:20']),20:('Event',['u16','str','c:TypeDefOrRef']),
21:('PropertyMap',['i:2','i:23']),22:('PropertyPtr',['i:23']),
23:('Property',['u16','str','blob']),24:('MethodSemantics',['u16','i:6','c:HasSemantics']),
25:('MethodImpl',['i:2','c:MethodDefOrRef','c:MethodDefOrRef']),
26:('ModuleRef',['str']),27:('TypeSpec',['blob']),
28:('ImplMap',['u16','c:MemberForwarded','str','i:26']),29:('FieldRVA',['u32','i:4']),
30:('ENCLog',['u32','u32']),31:('ENCMap',['u32']),
32:('Assembly',['u32','u16','u16','u16','u16','u32','blob','str','str']),
33:('AssemblyProcessor',['u32']),34:('AssemblyOS',['u32','u32','u32']),
35:('AssemblyRef',['u16','u16','u16','u16','u32','blob','str','str','blob']),
36:('AssemblyRefProcessor',['u32','i:35']),37:('AssemblyRefOS',['u32','u32','u32','i:35']),
38:('File',['u32','str','blob']),39:('ExportedType',['u32','u32','str','str','c:Implementation']),
40:('ManifestResource',['u32','u32','str','c:Implementation']),
41:('NestedClass',['i:2','i:2']),42:('GenericParam',['u16','u16','c:TypeOrMethodDef','str']),
43:('MethodSpec',['c:MethodDefOrRef','blob']),44:('GenericParamConstraint',['i:42','c:TypeDefOrRef'])
}
ES={0x01:'void',0x02:'bool',0x03:'char',0x04:'sbyte',0x05:'byte',0x06:'short',0x07:'ushort',0x08:'int',0x09:'uint',0x0a:'long',0x0b:'ulong',0x0c:'float',0x0d:'double',0x0e:'string',0x16:'typedref',0x18:'nint',0x19:'nuint',0x1c:'object'}

def ui(data,o,n): return int.from_bytes(data[o:o+n],'little')
def compressed(data,o):
 a=data[o]
 if a&0x80==0:return a,o+1
 if a&0xc0==0x80:return ((a&0x3f)<<8)|data[o+1],o+2
 return ((a&0x1f)<<24)|(data[o+1]<<16)|(data[o+2]<<8)|data[o+3],o+4
class Cli:
 def __init__(self,file):
  self.filename=file; self.data=open(file,'rb').read(); d=self.data
  pe=ui(d,0x3c,4);assert d[pe:pe+4]==b'PE\0\0'
  count=ui(d,pe+6,2); optional=pe+24
  magic=ui(d,optional,2); h=optional+(112 if magic==0x20b else 96)
  cli_rva=ui(d,h+14*8,4)
  sections=optional+ui(d,pe+20,2)
  self.sections=[]
  for i in range(count):
   p=sections+i*40; name=d[p:p+8].split(b'\0')[0].decode('ascii','replace')
   vsize,va,sz,ptr=struct.unpack_from('<IIII',d,p+8)
   self.sections.append((name,va,max(vsize,sz),ptr))
  co=self.rva(cli_rva);mdrva=ui(d,co+8,4);mdo=self.rva(mdrva)
  assert d[mdo:mdo+4]==b'BSJB'
  version_len=ui(d,mdo+12,4); self.version=d[mdo+16:mdo+16+version_len].rstrip(b'\0').decode('utf8','replace')
  ptr=(mdo+16+version_len+3)&~3
  flags,streams=struct.unpack_from('<HH',d,ptr);ptr+=4
  self.stream={}
  for _ in range(streams):
   ofs,sz=struct.unpack_from('<II',d,ptr)
   e=d.index(0,ptr+8);name=d[ptr+8:e].decode('ascii')
   self.stream[name]=d[mdo+ofs:mdo+ofs+sz]
   ptr=(e+4)&~3
  self.strings=self.stream['#Strings'];self.blob=self.stream['#Blob'];self.guid=self.stream['#GUID']
  s=self.stream.get('#~') or self.stream.get('#-'); assert s
  self.heapSizes=s[6];valid=ui(s,8,8)
  p=24; self.counts={}
  for i in range(64):
   if valid>>i&1:self.counts[i]=ui(s,p,4);p+=4
  self.table={};self.rows={}
  for i in range(64):
   if i not in self.counts:continue
   if i not in SCHEMA: raise ValueError('unknown table %d'%i)
   nam,cols=SCHEMA[i]
   n=self.counts[i];size=sum(self.width(c) for c in cols)
   self.table[i]=(p,size,n)
   self.rows[i]=[self.row(s,p+j*size,cols) for j in range(n)]
   p+=size*n
  self.method_owner={};self.field_owner={};self.nested={}
  for rid,(nt,owner) in enumerate(self.rows.get(41,[]),1):self.nested[nt]=owner
  for rid,t in enumerate(self.rows.get(2,[]),1):
   lastfield=(self.rows[2][rid][4] if rid<len(self.rows[2]) else len(self.rows.get(4,[]))+1)
   lastmeth=(self.rows[2][rid][5] if rid<len(self.rows[2]) else len(self.rows.get(6,[]))+1)
   for x in range(t[4],lastfield):self.field_owner[x]=rid
   for x in range(t[5],lastmeth):self.method_owner[x]=rid
 def rva(self,rva):
  for nam,va,sz,ptr in self.sections:
   if va<=rva<va+sz:return ptr+(rva-va)
  return rva
 def width(self,typ):
  if typ in ('u8',):return 1
  if typ in ('u16',):return 2
  if typ in ('u32',):return 4
  if typ in ('str','guid','blob'):
   bit={'str':0,'guid':1,'blob':2}[typ]
   return 4 if self.heapSizes>>bit&1 else 2
  if typ.startswith('i:'):return 2 if self.counts.get(int(typ[2:]),0)<65536 else 4
  if typ.startswith('c:'):
   tabs,bits=CODED[typ[2:]]
   return 2 if max(self.counts.get(j,0) for j in tabs if j is not None)<1<<(16-bits) else 4
  raise KeyError(typ)
 def row(self,s,p,cols):
  r=[]
  for c in cols:
   w=self.width(c);r.append(ui(s,p,w));p+=w
  return r
 def string(self,idx):
  if idx==0:return ''
  end=self.strings.find(b'\0',idx)
  return self.strings[idx:end].decode('utf8','replace')
 def blobstr(self,idx):
  if idx==0:return b''
  n,p=compressed(self.blob,idx);return self.blob[p:p+n]
 def fullname(self,rid):
  if rid<1 or rid>len(self.rows.get(2,[])):return '?'
  r=self.rows[2][rid-1];name=self.string(r[1]);ns=self.string(r[2])
  if rid in self.nested: return self.fullname(self.nested[rid])+'+'+name
  return ns+'.'+name if ns else name
 def refname(self,table,rid):
  if rid==0:return ''
  if table==2: return self.fullname(rid)
  if table==1:
   row=self.rows[1][rid-1];ns=self.string(row[2]);name=self.string(row[1]);return (ns+'.' if ns else '')+name
  if table==27:return '<'+self.typespec(rid)+'>'
  if table==6:return self.methodname(rid)
  if table==10:return self.membername(rid)
  return f'#{table}/{rid}'
 def decode_coded(self,name,value):
  tabs,bits=CODED[name];i=value &((1<<bits)-1);return (tabs[i] if i<len(tabs) else None), value>>bits
 def type_by_coded(self,value):
  t,r=self.decode_coded('TypeDefOrRef',value)
  return self.refname(t,r)
 def typespec(self,rid):
  b=self.blobstr(self.rows[27][rid-1][0]);return self.getsigtype(b,0)[0]
 def getsigtype(self,s,p):
  if p>=len(s):return '?',p
  e=s[p];p+=1
  if e in ES:return ES[e],p
  if e in (0x0f,0x10,0x1d,0x45):
   a,p=self.getsigtype(s,p)
   return ({0xf:'*',0x10:'&',0x1d:'[]',0x45:' pinned'}[e]+a if e==0x45 else a+{0xf:'*',0x10:'&',0x1d:'[]',0x45:' pinned'}[e]),p
  if e in (0x11,0x12):
   v,p=compressed(s,p);return self.type_by_coded(v),p
  if e in (0x13,0x1e):
   i,p=compressed(s,p);return ('!!' if e==0x1e else '!')+str(i),p
  if e==0x14:
   a,p=self.getsigtype(s,p);rank,p=compressed(s,p);num,p=compressed(s,p)
   for x in range(num):_,p=compressed(s,p)
   num,p=compressed(s,p)
   for x in range(num):_,p=compressed(s,p)
   return a+'['+','*max(0,rank-1)+']',p
  if e==0x15:
   kind=s[p];p+=1;cod,p=compressed(s,p);typ=self.type_by_coded(cod);n,p=compressed(s,p);args=[]
   for _ in range(n):q,p=self.getsigtype(s,p);args.append(q)
   return typ+'<'+','.join(args)+'>',p
  if e in (0x1f,0x20):
   cod,p=compressed(s,p);return self.getsigtype(s,p)
  if e==0x41:return self.getsigtype(s,p)
  if e==0x1b:return 'methodptr',len(s)
  return 'E'+hex(e),p
 def methodsig(self,rid):
  r=self.rows[6][rid-1];name=self.string(r[3]);s=self.blobstr(r[4]);
  try:return self.decode_method_sig(name,s)
  except Exception as ex:return name+'(? badsig)'
 def decode_method_sig(self,name,s):
  if not s:return f'? {name}(?)'
  p=0;flags=s[p];p+=1
  if flags & 0x10:arity,p=compressed(s,p)
  n,p=compressed(s,p);ret,p=self.getsigtype(s,p);args=[]
  for _ in range(n):
   a,p=self.getsigtype(s,p);args.append(a)
  return f'{ret} {name}('+', '.join(args)+')'
 def methodname(self,rid):
  owner=self.method_owner.get(rid);return self.fullname(owner)+'.'+self.methodsig(rid) if owner else '?'
 def membername(self,rid):
  row=self.rows[10][rid-1];t,r=self.decode_coded('MemberRefParent',row[0]);name=self.string(row[1]);sig=self.blobstr(row[2]);
  if sig and sig[0]&0x0f==6:return self.refname(t,r)+'::'+name+':field'
  return self.refname(t,r)+'::'+self.decode_method_sig(name,sig)
 def method_body(self,rid):
  r=self.rows[6][rid-1];rva=r[0]
  if not rva:return b''
  o=self.rva(rva);d=self.data
  if d[o]&3==2:hs=1;size=d[o]>>2
  elif d[o]&3==3:hs=(ui(d,o,2)>>12)*4;size=ui(d,o+4,4)
  else:return b''
  return d[o+hs:o+hs+size]
 def find(self,needle,limit=250):
  hits=[]
  for rid,t in enumerate(self.rows[2],1):
   tn=self.fullname(rid)
   for mid in range(t[5],self.rows[2][rid][5] if rid<len(self.rows[2]) else len(self.rows[6])+1):
    m=self.methodsig(mid)
    if needle.lower() in m.lower() or needle.lower() in tn.lower():
     hits.append((mid,tn,m))
     if len(hits)>=limit:return hits
  return hits

if __name__=='__main__':
 f=sys.argv[1];c=Cli(f);print('Version:',c.version,'types',len(c.rows[2]),'methods',len(c.rows[6]),'rows',[(SCHEMA[n][0],num) for n,num in c.counts.items() if n in (1,2,4,6,10,23)])
 for n in sys.argv[2:] or ['offline','Tick','Main.GameState','TimeScale']:
  print('\nFIND',n)
  for idx,t,m in c.find(n,300):print(' ',hex(0x06000000|idx),t,':',m)
