FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SchoolNewsBot.csproj", "./"]
RUN dotnet restore "SchoolNewsBot.csproj"
COPY . .
RUN dotnet publish "SchoolNewsBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SchoolNewsBot.dll"]