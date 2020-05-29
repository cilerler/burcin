Set-Location ".\src\Burcin.Host";
sc.exe create Burcin.Host DisplayName= "Burcin Service" start= auto binPath= ".\bin\Release\netcoreapp3.1\win10-x64\publish\Burcin.Host.exe --environment Production";
sc.exe failure Burcin.Host reset= 3600 reboot= "Burcin crashed -- rebooting machine" actions= restart/5000/restart/10000/reboot/60000;
