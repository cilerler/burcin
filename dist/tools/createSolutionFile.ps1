dotnet new sln;

dotnet sln Burcin.sln add src/Burcin.Api/Burcin.Api.csproj;

##if (ConsoleApplication)
dotnet sln Burcin.sln add src/Burcin.Console/Burcin.Console.csproj;
##endif

##if (EntityFramework)
dotnet sln Burcin.sln add src/Burcin.Models/Burcin.Models.csproj;
dotnet sln Burcin.sln add src/Burcin.Data/Burcin.Data.csproj;
dotnet sln Burcin.sln add src/Burcin.Migrations/Burcin.Migrations.csproj;
dotnet sln Burcin.sln add src/Burcin.Domain/Burcin.Domain.csproj;
##endif

##if (DockerSupport)
dotnet sln Burcin.sln add docker-compose.dcproj;
##endif

##if (TestFramework)
dotnet sln Burcin.sln add test/Burcin.Console.Tests/Burcin.Console.Tests.csproj;
##endif

