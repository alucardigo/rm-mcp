#!/bin/bash
# Start SQL Server in background, wait for it, run init script, then keep running.

/opt/mssql/bin/sqlservr &
SQLPID=$!

echo "Waiting for SQL Server..."
for i in $(seq 1 60); do
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" > /dev/null 2>&1
    if [ $? -eq 0 ]; then
        echo "SQL Server ready after ${i}s"
        break
    fi
    sleep 1
done

if [ -f /docker-entrypoint-initdb.d/init.sql ]; then
    echo "Running init script..."
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -i /docker-entrypoint-initdb.d/init.sql
    echo "Init script completed with exit code $?"
fi

wait $SQLPID
