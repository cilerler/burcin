Set-Location ".\src\Burcin.Console";
sc.exe create Burcin.Console DisplayName= "Burcin.Console Service" start= auto binPath= ".\bin\Release\netcoreapp2.1\win7-x64\publish\Bedia.Console.exe --environment Production";
sc.exe failure Burcin.Console reset= 3600 reboot= "Burcin.Console crashed -- rebooting machine" actions= restart/5000/restart/10000/reboot/60000;
