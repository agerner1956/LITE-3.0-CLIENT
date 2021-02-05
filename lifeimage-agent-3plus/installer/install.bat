sc create Lite3 binPath= %~dp0Lite3.exe
sc failure Lite3 actions= restart/60000/restart/60000/""/60000 reset= 86400
sc start Lite3 
sc config Lite3 start=auto