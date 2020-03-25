dotnet new sln;

dotnet sln Burcin.sln add src/Burcin.Api/Burcin.Api.csproj;

#--if (EntityFramework)
dotnet sln Burcin.sln add src/Burcin.Models/Burcin.Models.csproj;
dotnet sln Burcin.sln add src/Burcin.Data/Burcin.Data.csproj;
dotnet sln Burcin.sln add src/Burcin.Migrations/Burcin.Migrations.csproj;
#--endif

dotnet sln Burcin.sln add src/Burcin.Domain/Burcin.Domain.csproj;

#--if (DockerSupport)
dotnet sln Burcin.sln add docker-compose.dcproj;
#--endif

#--if (TestFramework)
dotnet sln Burcin.sln add test/Burcin.Domain.Tests/Burcin.Domain.Tests.csproj;
#--endif

# === DO NOT REMOVE THIS LINE ===
