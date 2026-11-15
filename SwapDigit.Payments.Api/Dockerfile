FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SwapDigit.Payments.Api/SwapDigit.Payments.Api.csproj SwapDigit.Payments.Api/
RUN dotnet restore SwapDigit.Payments.Api/SwapDigit.Payments.Api.csproj
COPY . .
RUN dotnet publish SwapDigit.Payments.Api/SwapDigit.Payments.Api.csproj -c Release -o /app/publish
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SwapDigit.Payments.Api.dll"]
