Set-Location ".\src\Burcin.Api";
sc.exe create Burcin.Api DisplayName= "Burcin Service" start= auto binPath= ".\bin\Release\netcoreapp3.1\win10-x64\publish\Burcin.Api.exe --environment Production";
sc.exe failure Burcin.Api reset= 3600 reboot= "Burcin crashed -- rebooting machine" actions= restart/5000/restart/10000/reboot/60000;
