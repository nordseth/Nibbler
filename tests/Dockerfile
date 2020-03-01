FROM registry.centos.org/dotnet/dotnet-31-runtime-centos7:latest

WORKDIR /opt/app-root/app
#COPY --chown=1001:0 . /opt/app-root/app
COPY . /opt/app-root/app
CMD dotnet aspnetcore-new.dll