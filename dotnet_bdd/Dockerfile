FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./

RUN dotnet build -c Release -o out

ENTRYPOINT ["dotnet", "out/dotnet_bdd.dll"]
