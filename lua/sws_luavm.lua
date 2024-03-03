local f,t=false,true
local FC,c,Frms,F,n,h,st,S,CS,sp,cs,G,C,ot,od,PG,PRG,tn,ts,s=0,property.getNumber("c"),{},{},"",f,f,{},{},0,0,{},{},f,f,"","",tonumber,tostring,string
for i=1,c do PG=PG..property.getText("p"..i)end c=1
function NT()local j=0 local i=PRG:sub(c):find(s.char(1))if i~=nil then j=PRG:sub(c,c+i-2)c=c+i return j end return 0 end
for i=1,#PG/2 do PRG=PRG..s.char(tn(PG:sub(i*2-1,i*2),16))end
FC=tn(NT())for i=1,FC do
local frm={"",{},{},{},{},f,1,{},f}frm[1]=NT()local pc=tn(NT())for j=1,pc do
frm[2][j]=tn(NT())end
NT()local uc=tn(NT())for j=0,uc-1 do frm[4][j]=NT()frm[5][j]=tn(NT())end
Frms[frm[1]]=frm end
NT()local cc=tn(NT())for i=0,cc-1 do
C[i]=NT()NT()end
ot=nil~=Frms["onTick"]od=Frms["onDraw"]~=nil
function THas(tb,val)if tb[0]==val then return t end for k,v in ipairs(tb)do if v==val then return t end end return f end
function Copy(a)local Frm={a[1],a[2],{},a[4],a[5],a[6],a[7],a[8],a[9]}for i=0,#a[3]do Frm[3][i]=a[3][i]end return Frm end
function Exec(otc)h=f
if not st then st=t elseif otc and ot then n="onTick"elseif not otc and od then n="onDraw"else return end
F=Copy(Frms[n])if n~=""then CS[1]=Copy(F)if THas(F[4],CS[0][1])then F[8][CS[0][1]]=0 end end
while true do
local PC=1+F[7]local i=F[2][PC-1]local I,D,E,NN,a,b,c=i>>18,i&131071,0,t,0,0,0
E=D
if(i&131072)>0 then D=-D NN=f end
if I<22 then sp=sp-2 a,b=S[sp+1],S[sp]elseif I<37 then sp=sp-1 a=S[sp]end
if I==38 then c=F[3][D]
elseif I==39 then c=CS[F[8][F[4][D]]][3][F[5][D]]
elseif I==40 then c=G[D]
elseif I==22 then F[3][D]=a
elseif I==23 then CS[F[8][F[4][D]]][3][F[5][D]]=a
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
elseif I==46 then PC=PC+D
elseif I==18 then if a==b then PC=PC+D end
elseif I==19 then if a~=b then PC=PC+D end
elseif I==20 then if a>b then PC=PC+D end
elseif I==21 then if a>=b then PC=PC+D end
elseif I==26 then if not a then PC=PC+D end
elseif I==27 then if a then PC=PC+D end
elseif I==28 then if not a then PC=PC+D sp=sp+1 end
elseif I==29 then if a then PC=PC+D sp=sp+1 end
elseif I==49 then sp=sp-1 local tb=S[sp]for j=1,E do sp=sp-2 tb[S[sp+1]]=S[sp]end if D<0 then S[sp]=tb sp=sp+1 end
elseif I==42 then c=D>0 and t or f
elseif I==31 then c=-a
elseif I==32 then c=not a
elseif I==35 then c={}for k,v in pairs(a)do c[#c+1]=k end
elseif I==36 then c=type(a)
elseif I==34 then c=#a
elseif I==50 then sp=sp-1 local cl=Copy(S[sp])cl[9]=NN if cl[6]then local out,arg={},{}for j=1,E do sp=sp-1 arg[j]=S[sp]end out=table.pack(cl[2](table.unpack(arg)))if D>0 then for j=1,#out do S[sp]=out[j]sp=sp+1 end end else for j=0,E-1 do sp=sp-1 cl[3][j]=S[sp]end CS[cs]=Copy(F)cs=cs+1 for j=cs-1,0,-1 do if THas(cl[4],CS[j][1])then cl[8][CS[j][1]]=j end end F[7]=PC PC=1 F=Copy(cl)end
elseif I==51 then cs=cs-1 if cs==1 and(ot or od)and n~=""then h=t end if not F[9]then sp=sp-D end F=Copy(CS[cs])PC=1+F[7]
elseif I==12 then c=ts(a)..ts(b)for j=1,D do sp=sp-1 c=c..ts(S[sp])end
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
elseif I==43 then c=nil
elseif I==25 then S[sp],S[sp+1]=a,a sp=sp+2
elseif I==48 then h=t
elseif I==30 then debug.log(a)
elseif I==47 then local cln,cl,fn=C[D],{},_ENV cl[1]=cln cl[3]={}cl[6]=cln:find("lua ",1,true)~=nil if cl[6]then cln=cln:gmatch("%S+")cln()for v in cln do fn=fn[v]end cl[2]=fn else cl=Copy(Frms[cln])end S[sp]=cl sp=sp+1 end
if I<18 or(I>30 and I<45)then S[sp]=c sp=sp+1 end
F[7]=PC
if h then break end
end
if not ot and not od then return else if n==""then CS[0]=Copy(F)end cs=2 end end
function onTick()Exec(t)end
function onDraw()Exec(f)end