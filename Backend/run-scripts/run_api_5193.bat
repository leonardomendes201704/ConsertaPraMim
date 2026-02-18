@echo off
set ASPNETCORE_ENVIRONMENT=Development
set CPM_FIREBASE_SERVICE_ACCOUNT_PATH=C:\secrets\firebase\consertapramimcliente-firebase-adminsdk-fbsvc-eb0eb9ce32.json
cd /d C:\Leonardo\Labs\ConsertaPraMimWeb\Backend\src
dotnet run --project ConsertaPraMim.API\ConsertaPraMim.API.csproj --launch-profile http --urls http://0.0.0.0:5193
