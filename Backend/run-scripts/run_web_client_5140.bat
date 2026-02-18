@echo off
set ASPNETCORE_ENVIRONMENT=Development
cd /d C:\Leonardo\Labs\ConsertaPraMimWeb\Backend\src
dotnet run --project ConsertaPraMim.Web.Client\ConsertaPraMim.Web.Client.csproj --launch-profile http --urls http://0.0.0.0:5140
