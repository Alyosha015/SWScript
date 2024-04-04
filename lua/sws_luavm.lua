local f,t,N,k,d=false,true,nil,"onTick","onDraw"
local FC,c,Frms,F,n,h,st,S,CS,sp,cs,G,C,ot,od,PG,PRG,tn,ts,s=0,property.getNumber("c"),{},{},"",f,f,{},{},0,0,{},{},f,f,"","",tonumber,tostring,string
for i=1,c do PG=PG..property.getText("p"..i)end c=1
function NT()local j=0 local i=PRG:sub(c):find(s.char(1))if i~=N then j=PRG:sub(c,c+i-2)c=c+i return j end return 0 end
for i=1,#PG/2 do PRG=PRG..s.char(tn(PG:sub(i*2-1,i*2),16))end
FC=tn(NT())for i=1,FC do
local _={"",{},{},{},{},f,1,{},{},f}_[1]=NT()local pc=tn(NT())for j=1,pc do
_[2][j]=tn(NT())end
NT()local uc=tn(NT())for j=0,uc-1 do _[4][j]=NT()_[5][j]=tn(NT())end
Frms[_[1]]=_ end
NT()local cc=tn(NT())for i=0,cc-1 do
C[i]=NT()NT()end
ot=N~=Frms[k]od=N~=Frms[d]
function TC(T,V)for k,v in pairs(T)do if v==V then return t end end return f end
function Cy(a)local _={a[1],a[2],{},a[4],a[5],a[6],a[7],a[8],{},a[10]}for i=0,#a[3]do _[3][i]=a[3][i]end for i=0,#a[9]do _[9][i]=a[9][i]end return _ end
function Ex(otc)h=f
if not st then cs=1 st=t elseif otc and ot then n=k elseif not otc and od then n=d else return end
F=Cy(Frms[n])if n~=""then CS[1]=Cy(F)if TC(F[4],CS[0][1])then F[8][CS[0][1]]=0 end else CS[0]=Cy(F)end
while true do
local P=1+F[7]i=F[2][P-1]local I,D,E,NN,a,b,c=i>>18,i&131071,0,t,0,0,0
E=D
if(i&131072)>0 then D=-D NN=f end
if I<22 then sp=sp-2 a,b=S[sp+1],S[sp]elseif I<37 then sp=sp-1 a=S[sp]end
if I==38 then c=F[3][D]
elseif I==39 then c=N~=F[8][F[4][D]]and CS[F[8][F[4][D]]][3][F[5][D]]or F[9][D]
elseif I==40 then c=G[D]
elseif I==22 then F[3][D]=a
elseif I==23 then if N~=F[8][F[4][D]]then CS[F[8][F[4][D]]][3][F[5][D]]=a else F[9][D]=a end
elseif I==24 then G[D]=a
elseif I==45 then S[sp-1]=D+S[sp-1]
elseif I==41 then c=D
elseif I==0 then c=a+b
elseif I==1 then c=a-b
elseif I==2 then c=a*b
elseif I==3 then c=a/b
elseif I==13 then c=a==b
elseif I==14 then c=a~=b
elseif I==15 then c=a<b
elseif I==16 then c=a<=b
elseif I==17 then c=b[a]
elseif I==46 then P=P+D
elseif I==18 then if a==b then P=P+D end
elseif I==19 then if a~=b then P=P+D end
elseif I==20 then if a>b then P=P+D end
elseif I==21 then if a>=b then P=P+D end
elseif I==26 then if not a then P=P+D end
elseif I==27 then if a then P=P+D end
elseif I==28 then if not a then P=P+D sp=sp+1 end
elseif I==29 then if a then P=P+D sp=sp+1 end
elseif I==49 then sp=sp-1 local tb=S[sp]for j=1,E do sp=sp-2 tb[S[sp+1]]=S[sp]end if D<0 then S[sp]=tb sp=sp+1 end
elseif I==42 then c=D>0 and t or f
elseif I==31 then c=-a
elseif I==32 then c=not a
elseif I==35 then c={}for k,v in pairs(a)do c[#c+1]=k end
elseif I==36 then c=type(a)
elseif I==34 then c=#a
elseif I==50 then sp=sp-1 local cl=Cy(S[sp])cl[10]=NN if cl[6]then local out,arg={},{}for j=1,E do sp=sp-1 arg[j]=S[sp]end out=table.pack(cl[2](table.unpack(arg)))if D>0 then for j=1,#out do S[sp]=out[j]sp=sp+1 end end else for j=0,E-1 do sp=sp-1 cl[3][j]=S[sp]end CS[cs]=Cy(F)cs=cs+1 for j=cs-1,0,-1 do if TC(cl[4],CS[j][1])then cl[8][CS[j][1]]=j end end F[7]=P P=1 F=Cy(cl)end
elseif I==51 then cs=cs-1 if cs==1 and(ot or od)and n~=""then h=t end if not F[10]then sp=sp-D end F=Cy(CS[cs])P=1+F[7]elseif I==12 then c=ts(a)..ts(b)for j=1,D do sp=sp-1 c=c..ts(S[sp])end
elseif I==4 then c=a//b
elseif I==5 then c=a^b
elseif I==6 then c=a%b
elseif I==33 then c=~a
elseif I==7 then c=a&b
elseif I==8 then c=a|b
elseif I==9 then c=a~b
elseif I==10 then c=a<<b
elseif I==11 then c=a>>b
elseif I==44 then c={}
elseif I==37 then c=C[D]
elseif I==43 then c=N
elseif I==25 then S[sp],S[sp+1]=a,a sp=sp+2
elseif I==48 then h=t
elseif I==30 then debug.log(a)
elseif I==47 then local cn,fn,cl=C[D],_ENV,{}cl[1]=cn cl[3]={}cl[9]={}cl[6]=N~=cn:find("lua ",1,t)if cl[6]then cn=cn:gmatch("%S+")cn()for v in cn do fn=fn[v]end cl[2]=fn else cl=Cy(Frms[cn])CS[cs]=Cy(F)for j=cs,1,-1 do if TC(cl[4],CS[j][1])then for k=0,#cl[4]do if cl[4][k]==CS[j][1]then cl[9][k]=CS[j][3][cl[5][k]]end end end end end S[sp]=cl sp=sp+1 end
if I<18 or(I>30 and I<45)then S[sp]=c sp=sp+1 end
F[7]=P
if h then break end
end
if ot or od then cs=2 else return end end
function onTick()Ex(t)end
function onDraw()Ex(f)end