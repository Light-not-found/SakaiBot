FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/SakaiBot/SakaiBot.csproj", "src/SakaiBot/"]
RUN dotnet restore "src/SakaiBot/SakaiBot.csproj"

COPY . .
WORKDIR /src/src/SakaiBot
RUN dotnet publish "SakaiBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SakaiBot.dll"]
