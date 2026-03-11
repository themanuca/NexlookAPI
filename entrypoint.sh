#!/bin/sh
export ASPNETCORE_HTTP_PORTS=${PORT:-8080}
exec dotnet NexlookAPI.dll
