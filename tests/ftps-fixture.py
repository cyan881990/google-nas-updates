import socket,socketserver,ssl,sys
port,cert,key,mode=int(sys.argv[1]),sys.argv[2],sys.argv[3],sys.argv[4]
ctx=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
ctx.load_cert_chain(cert,key)
class Handler(socketserver.BaseRequestHandler):
 def handle(self):
  conn=self.request; passive=None; private=False
  try:
   if mode=='implicit': conn=ctx.wrap_socket(conn,server_side=True)
   def send(s): conn.sendall((s+'\r\n').encode())
   send('220 Loopback FTPS fixture')
   while True:
    line=b''
    while not line.endswith(b'\n'):
     chunk=conn.recv(1)
     if not chunk:return
     line+=chunk
    cmd,_,arg=line.decode().strip().partition(' ');cmd=cmd.upper()
    if cmd=='AUTH':
     send('234 TLS ready');conn=ctx.wrap_socket(conn,server_side=True)
    elif cmd=='USER':send('331 Password required')
    elif cmd=='PASS':send('230 Logged in' if arg=='FixtureOnly!12' else '530 Login incorrect')
    elif cmd=='PBSZ':send('200 PBSZ accepted')
    elif cmd=='PROT':private=arg.upper()=='P';send('200 Protection accepted')
    elif cmd=='FEAT':send('211-Features\r\n UTF8\r\n MLST type*;size*;modify*;\r\n EPSV\r\n AUTH TLS\r\n211 End')
    elif cmd in ('OPTS','TYPE','NOOP','CWD'):send('200 OK')
    elif cmd=='SYST':send('215 UNIX Type: L8')
    elif cmd=='PWD':send('257 "/"')
    elif cmd in ('EPSV','PASV'):
     if passive:passive.close()
     passive=socket.socket();passive.bind(('127.0.0.1',0));passive.listen(1);p=passive.getsockname()[1]
     send('229 Entering Extended Passive Mode (|||%s|)'%p if cmd=='EPSV' else '227 Entering Passive Mode (127,0,0,1,%s,%s)'%(p//256,p%256))
    elif cmd in ('MLSD','LIST','RETR'):
     send('150 Opening data connection');data,_=passive.accept()
     if private:data=ctx.wrap_socket(data,server_side=True)
     payload=b'type=file;size=15;modify=20260101000000; fixture.txt\r\n' if cmd=='MLSD' else b'-rw-r--r-- 1 user group 15 Jan 01 2026 fixture.txt\r\n' if cmd=='LIST' else b'Fixture content'
     data.sendall(payload)
     try:data.unwrap() if private else None
     except (OSError,ssl.SSLError):pass
     data.close();passive.close();passive=None;send('226 Transfer complete')
    elif cmd=='MDTM':send('213 20260101000000')
    elif cmd=='SIZE':send('213 15')
    elif cmd=='QUIT':send('221 Bye');return
    else:send('502 Not implemented')
  except (OSError,ssl.SSLError):pass
  finally:
   if passive:passive.close()
   conn.close()
class Server(socketserver.ThreadingTCPServer):
 allow_reuse_address=True
 daemon_threads=True
with Server(('127.0.0.1',port),Handler) as server:server.serve_forever()
