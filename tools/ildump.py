from cli_inspect import Cli,compressed
import struct,sys
SHORT=['br.s','brfalse.s','brtrue.s','beq.s','bge.s','bgt.s','ble.s','blt.s','bne.un.s','bge.un.s','bgt.un.s','ble.un.s','blt.un.s']
LONG=['br','brfalse','brtrue','beq','bge','bgt','ble','blt','bne.un','bge.un','bgt.un','ble.un','blt.un']
NONE={
0x00:'nop',0x01:'break',0x02:'ldarg.0',0x03:'ldarg.1',0x04:'ldarg.2',0x05:'ldarg.3',
0x06:'ldloc.0',0x07:'ldloc.1',0x08:'ldloc.2',0x09:'ldloc.3',
0x0a:'stloc.0',0x0b:'stloc.1',0x0c:'stloc.2',0x0d:'stloc.3',
0x14:'ldnull',0x15:'ldc.i4.m1',0x16:'ldc.i4.0',0x17:'ldc.i4.1',0x18:'ldc.i4.2',0x19:'ldc.i4.3',0x1a:'ldc.i4.4',0x1b:'ldc.i4.5',0x1c:'ldc.i4.6',0x1d:'ldc.i4.7',0x1e:'ldc.i4.8',
0x25:'dup',0x26:'pop',0x2a:'ret',
0x46:'ldind.i1',0x47:'ldind.u1',0x48:'ldind.i2',0x49:'ldind.u2',0x4a:'ldind.i4',0x4b:'ldind.u4',0x4c:'ldind.i8',0x4d:'ldind.i',0x4e:'ldind.r4',0x4f:'ldind.r8',0x50:'ldind.ref',
0x51:'stind.ref',0x52:'stind.i1',0x53:'stind.i2',0x54:'stind.i4',0x55:'stind.i8',0x56:'stind.r4',0x57:'stind.r8',
0x58:'add',0x59:'sub',0x5a:'mul',0x5b:'div',0x5c:'div.un',0x5d:'rem',0x5e:'rem.un',0x5f:'and',0x60:'or',0x61:'xor',0x62:'shl',0x63:'shr',0x64:'shr.un',0x65:'neg',0x66:'not',
0x67:'conv.i1',0x68:'conv.i2',0x69:'conv.i4',0x6a:'conv.i8',0x6b:'conv.r4',0x6c:'conv.r8',0x6d:'conv.u4',0x6e:'conv.u8',
0x76:'conv.r.un',0x7a:'throw',0x8e:'ldlen',
0x90:'ldelem.i1',0x91:'ldelem.u1',0x92:'ldelem.i2',0x93:'ldelem.u2',0x94:'ldelem.i4',0x95:'ldelem.u4',0x96:'ldelem.i8',0x97:'ldelem.i',0x98:'ldelem.r4',0x99:'ldelem.r8',0x9a:'ldelem.ref',
0x9b:'stelem.i',0x9c:'stelem.i1',0x9d:'stelem.i2',0x9e:'stelem.i4',0x9f:'stelem.i8',0xa0:'stelem.r4',0xa1:'stelem.r8',0xa2:'stelem.ref',
0xb3:'conv.ovf.i1',0xb4:'conv.ovf.u1',0xb5:'conv.ovf.i2',0xb6:'conv.ovf.u2',0xb7:'conv.ovf.i4',0xb8:'conv.ovf.u4',0xb9:'conv.ovf.i8',0xba:'conv.ovf.u8',
0xc3:'ckfinite',0xd1:'conv.u2',0xd2:'conv.u1',0xd3:'conv.i',0xd4:'conv.ovf.i',0xd5:'conv.ovf.u',0xd6:'add.ovf',0xd7:'add.ovf.un',0xd8:'mul.ovf',0xd9:'mul.ovf.un',0xda:'sub.ovf',0xdb:'sub.ovf.un',0xdc:'endfinally',0xdf:'stind.i',0xe0:'conv.u'
}
U8={0x0e:'ldarg.s',0x0f:'ldarga.s',0x10:'starg.s',0x11:'ldloc.s',0x12:'ldloca.s',0x13:'stloc.s',0x1f:'ldc.i4.s'}
I32={0x20:'ldc.i4'}
I64={0x21:'ldc.i8'}
FLOAT={0x22:'ldc.r4',0x23:'ldc.r8'}
TOK={
0x27:'jmp',0x28:'call',0x29:'calli',0x6f:'callvirt',0x70:'cpobj',0x71:'ldobj',0x72:'ldstr',0x73:'newobj',0x74:'castclass',0x75:'isinst',0x79:'unbox',0x7b:'ldfld',0x7c:'ldflda',0x7d:'stfld',0x7e:'ldsfld',0x7f:'ldsflda',0x80:'stsfld',0x81:'stobj',0x8c:'box',0x8d:'newarr',0x8f:'ldelema',0xa3:'ldelem',0xa4:'stelem',0xa5:'unbox.any',0xc2:'refanyval',0xc6:'mkrefany',0xd0:'ldtoken'
}
SHORTOP={0xde:'leave.s'}
LONGOP={0xdd:'leave'}
FENONE={0x00:'arglist',0x01:'ceq',0x02:'cgt',0x03:'cgt.un',0x04:'clt',0x05:'clt.un',0x0f:'localloc',0x11:'endfilter',0x13:'volatile.',0x14:'tail.',0x17:'cpblk',0x18:'initblk',0x1a:'rethrow',0x1d:'refanytype',0x1e:'readonly.'}
FE2={0x09:'ldarg',0x0a:'ldarga',0x0b:'starg',0x0c:'ldloc',0x0d:'ldloca'}
FE4={0x06:'ldftn',0x07:'ldvirtftn',0x15:'initobj',0x16:'constrained.',0x1c:'sizeof'}
FE1={0x12:'unaligned.'}

def fieldname(c,rid):
 row=c.rows[4][rid-1];return c.fullname(c.field_owner.get(rid))+'::'+c.string(row[1])
def tokenname(c,tok):
 t=tok>>24;r=tok&0xffffff
 try:
  if t==0x6:return c.methodname(r)
  if t==0xa:return c.membername(r)
  if t==0x2b:
   row=c.rows[43][r-1];a,b=c.decode_coded('MethodDefOrRef',row[0]);return f'{c.refname(a,b)}<methodspec>'
  if t==0x4:return fieldname(c,r)
  if t in (1,2,27):return c.refname(t,r)
  if t==0x70:
   b=c.stream['#US'];n,p=compressed(b,r);return repr(b[p:p+n-1].decode('utf-16le','replace'))
 except Exception:return hex(tok)
 return hex(tok)

def dump(c,rid,only_calls=False):
 b=c.method_body(rid)
 print(c.methodname(rid),'IL length',len(b));p=0;lines=[]
 while p<len(b):
  s=p;v=b[p];p+=1
  if v==0xfe:
   v=b[p];p+=1
   if v in FENONE:op=FENONE[v];arg=''
   elif v in FE2:op=FE2[v];arg=str(int.from_bytes(b[p:p+2],'little'));p+=2
   elif v in FE4:op=FE4[v];arg=tokenname(c,int.from_bytes(b[p:p+4],'little'));p+=4
   elif v in FE1:op=FE1[v];arg=str(b[p]);p+=1
   else:op='UNKNOWNFE'+hex(v);arg=''
  elif v in NONE:op=NONE[v];arg=''
  elif v in U8:op=U8[v];arg=str(int.from_bytes(b[p:p+1],'little',signed=v==0x1f));p+=1
  elif v in I32:op=I32[v];arg=str(int.from_bytes(b[p:p+4],'little',signed=True));p+=4
  elif v in I64:op=I64[v];arg=str(int.from_bytes(b[p:p+8],'little',signed=True));p+=8
  elif v in FLOAT:op=FLOAT[v];arg=str(struct.unpack_from('<f' if v==0x22 else '<d',b,p)[0]);p+=4 if v==0x22 else 8
  elif v in TOK:op=TOK[v];arg=tokenname(c,int.from_bytes(b[p:p+4],'little'));p+=4
  elif v in SHORTOP:op=SHORTOP[v];a=int.from_bytes(b[p:p+1],'little',signed=True);p+=1;arg='IL_%04x'%(p+a)
  elif v in LONGOP:op=LONGOP[v];a=int.from_bytes(b[p:p+4],'little',signed=True);p+=4;arg='IL_%04x'%(p+a)
  elif 0x2b<=v<=0x37:op=SHORT[v-0x2b];a=int.from_bytes(b[p:p+1],'little',signed=True);p+=1;arg='IL_%04x'%(p+a)
  elif 0x38<=v<=0x44:op=LONG[v-0x38];a=int.from_bytes(b[p:p+4],'little',signed=True);p+=4;arg='IL_%04x'%(p+a)
  elif v==0x45:
   n=int.from_bytes(b[p:p+4],'little');p+=4
   shifts=list(struct.unpack_from('<'+'i'*n,b,p));p+=4*n;op='switch';arg=', '.join('IL_%04x'%(p+a) for a in shifts)
  else:op='UNKNOWN'+hex(v);arg=''
  lines.append(f' IL_{s:04x}: {op} {arg}')
  if p<=s:raise RuntimeError('not advancing')
 if only_calls: lines=[line for line in lines if any(k in line for k in ['call','ldfld','stfld','ldsfld','stsfld','newobj','ldstr'])]
 print('\n'.join(lines))

if __name__=='__main__':
 c=Cli(sys.argv[1]);opts=sys.argv[2:]
 only_calls='--calls' in opts
 for q in opts:
  if q.startswith('--'):continue
  if q.startswith('0x'):dump(c,int(q,16)&0xffffff,only_calls)
  elif q.isdecimal():dump(c,int(q),only_calls)
  else:
   matches=[r for r,t,m in c.find(q) if q.lower() in m.lower() or q.lower() in t.lower()]
   if len(matches)>20:print('many matches',len(matches),q);continue
   for rid in matches:dump(c,rid,only_calls)
