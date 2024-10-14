ARG DOTNET_6=6.0.410
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_6} AS base-env
WORKDIR /app
COPY out .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
CMD ["/app/BuildJobApi"]