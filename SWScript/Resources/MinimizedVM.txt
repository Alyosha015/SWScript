local s,o,w,Z,W,i,O=true,false,nil,"onTick","onDraw",property,"SWS"local p,F,v,A,e,E,j,m,a,q,l,Q,I,H,L,K,J,R,n,x=0,0,i.getNumber(O),{},{},o,{},{},0,0,{},{},o,o,"","",tonumber,tostring,string.char,""for l=1,v do
L=L..i.getText(O..l)end
v=1
function B(i,D)D=K:sub(v):find(n(1))if D then
i=K:sub(v,v+D-2)v=v+D
end
return i or 0
end
function S(z,T,i)z=J(B())i=(z==2 and J(B())or B())if z<1 then i=w
elseif z==1 then i=i==O
elseif z==4 then
T={}i=J(i)for l=1,i do
T[S()]=S()end
i=T
end
return i
end
for l=1,#L,2 do
K=K..n(J(L:sub(l,l+1),16))end
p=J(B())for l=1,p do
e={"",{},{},{},{},o,1,{},0,0}e[1]=B()F=J(B())for k=1,F do
e[2][k]=J(B())end
e[9]=J(B())F=J(B())for k=0,F-1 do
e[4][k]=B()e[5][k]=J(B())end
A[e[1]]=e
end
B()p=J(B())for l=0,p-1 do
Q[l]=S()end
I=w~=A[Z]H=w~=A[W]i=not I function r(n,V,i)i={}for l=1,10 do
i[l]=n[l]end
if not V then i[3]={}for l=0,n[9]do
i[3][l]=n[3][l]end end
return i
end
function ai(t,f,C)if not h then
h=s
elseif t and I then
i=s
x=Z
elseif not t and H and i then
x=W
else
return
end
E=o
e=r(A[x])if x~=""then
m[1]=r(e)for k=0,#e[4]do if e[4][k]==m[0][1]then e[8][k]=m[0][3]end end
else
m[0]=r(e)end
while 1 do
f=1+e[7]C=e[2][f-1]local _,d,b,h,c=C>>18,C&131071
if(C&131072)>0 then
d=-d
end
if _<22 then
a=a-2
b,h=j[a+1],j[a]elseif _<38 then
a=a-1
b=j[a]end
if _==39 then c=e[3][d]elseif _==40 then c=e[8][d][e[5][d]]elseif _==41 then c=l[d]elseif _==22 then e[3][d]=b
elseif _==23 then e[8][d][e[5][d]]=b
elseif _==24 then l[d]=b
elseif _==46 then j[a-1]=d+j[a-1]elseif _==42 then c=d
elseif _==38 then c=Q[d]elseif _==0 then c=b+h
elseif _==1 then c=b-h
elseif _==2 then c=b*h
elseif _==3 then c=b/h
elseif _==13 then c=b==h
elseif _==14 then c=b~=h
elseif _==15 then c=b<h
elseif _==16 then c=b<=h
elseif _==17 then c=h[b]elseif _==47 then f=f+d
elseif _==31 then f=f+d+b
elseif _==18 then f=f+(b==h and d or 0)elseif _==19 then f=f+(b~=h and d or 0)elseif _==20 then f=f+(b>h and d or 0)elseif _==21 then f=f+(b>=h and d or 0)elseif _==26 then f=f+(b and 0 or d)elseif _==27 then f=f+(b and d or 0)elseif _==28 then if not b then f=f+d a=a+1 end
elseif _==29 then if b then f=f+d a=a+1 end
elseif _==50 then a=a-1 p=j[a]for k=1,C&131071 do a=a-2 p[j[a+1]]=j[a]end if(C&131072)>0 then j[a]=p a=a+1 end
elseif _==43 then c=d>0
elseif _==32 then c=-b
elseif _==33 then c=not b
elseif _==36 then c={}for y,Y in pairs(b)do c[#c+1]=y end
elseif _==37 then c=type(b)elseif _==35 then c=#b
elseif _==51 then
a=a-1
c=type(j[a])=="function"local i,V,b,F,g={},d&255,(d&65280)>>8,{}if not c then
g=r(j[a])g[10]=b
end
if c or g[6]then
for k=1,V do
a=a-1
F[k]=j[a]end
i=table.pack((c and j[a]or g[2])(table.unpack(F)))for k=1,b do
j[a]=i[k]a=a+1
end
else
for k=0,V-1 do
a=a-1
g[3][k]=j[a]end
f=1
m[q]=r(e,s)q=q+1
e=g end
elseif _==4 then c=b//h
elseif _==5 then c=b^h
elseif _==6 then c=b%h
elseif _==34 then c=~b
elseif _==7 then c=b&h
elseif _==8 then c=b|h
elseif _==9 then c=b~h
elseif _==10 then c=b<<h
elseif _==11 then c=b>>h
elseif _==12 then c=R(b)..R(h)for k=1,d do a=a-1 c=c..R(j[a])end
elseif _==52 then
q=q-1
E=q==1 and(I or H)and x~=""if 1>e[10]then
a=a-d
end
for k=d+1,e[10]do
j[a]=w
a=a+1
end
e=r(m[q],s)f=1+e[7]elseif _==45 then c={}elseif _==44 then c=w
elseif _==25 then j[a],j[a+1]=b,b a=a+2
elseif _==30 then debug.log(b)elseif _==48 then
local u,N,g=Q[d],_ENV,{}g[1]=u
g[3]={}g[6]=w~=u:find("lua ",1,s)if g[6]then
u=u:gmatch("%S+")u()for Y in u do
N=N[Y]end
g[2]=N
g[9]=0
else
g=r(A[u])m[q]=r(e,s)for k=0,q do
for y=0,#g[4]do
if g[4][y]==m[k][1]then
g[8][y]=m[k][3]end
end
end
end
j[a]=g
a=a+1
else E=s end
if _<18 or(_>30 and _<46)then
j[a]=c
a=a+1
end
e[7]=f
if E then break end
end
if I or H then
q=2
if x==""then
m[0]=r(e)end
else
return
end
end
function onTick()ai(s)end
function onDraw()ai(o)end