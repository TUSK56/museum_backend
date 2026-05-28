param(
    [string]$SourceConnectionString = "Server=localhost,1433;Database=VirtualMuseumDB;User ID=sa;Password=VmStrongP@ssw0rd123;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true;",
    [string]$DestinationConnectionString = "Server=db48858.databaseasp.net;Database=db48858;User Id=db48858;Password=cL+5B?6g!2oA;Encrypt=False;MultipleActiveResultSets=True;"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Data

function Open-SqlConnection {
    param([string]$ConnectionString)
    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $connection.Open()
    return $connection
}

function Get-TableList {
    param([System.Data.SqlClient.SqlConnection]$Connection)
    $command = $Connection.CreateCommand()
    $command.CommandText = @"
SELECT
    s.name AS SchemaName,
    t.name AS TableName
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND t.name <> '__EFMigrationsHistory'
ORDER BY s.name, t.name;
"@

    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
    $table = New-Object System.Data.DataTable
    [void]$adapter.Fill($table)
    return $table
}

function Disable-Constraints {
    param([System.Data.SqlClient.SqlConnection]$Connection)
    $command = $Connection.CreateCommand()
    $command.CommandText = @"
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE [' + s.name + N'].[' + t.name + N'] NOCHECK CONSTRAINT ALL;'
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;
EXEC sp_executesql @sql;
"@
    [void]$command.ExecuteNonQuery()
}

function Enable-Constraints {
    param([System.Data.SqlClient.SqlConnection]$Connection)
    $command = $Connection.CreateCommand()
    $command.CommandText = @"
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE [' + s.name + N'].[' + t.name + N'] WITH CHECK CHECK CONSTRAINT ALL;'
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;
EXEC sp_executesql @sql;
"@
    [void]$command.ExecuteNonQuery()
}

function Clear-DestinationTables {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [System.Data.DataTable]$Tables
    )

    for ($i = $Tables.Rows.Count - 1; $i -ge 0; $i--) {
        $schemaName = $Tables.Rows[$i]["SchemaName"]
        $tableName = $Tables.Rows[$i]["TableName"]
        $command = $Connection.CreateCommand()
        $command.CommandText = "DELETE FROM [$schemaName].[$tableName];"
        [void]$command.ExecuteNonQuery()
    }
}

function Copy-TableData {
    param(
        [System.Data.SqlClient.SqlConnection]$Source,
        [System.Data.SqlClient.SqlConnection]$Destination,
        [string]$SchemaName,
        [string]$TableName
    )

    $selectCommand = $Source.CreateCommand()
    $selectCommand.CommandText = "SELECT * FROM [$SchemaName].[$TableName];"
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $selectCommand
    $data = New-Object System.Data.DataTable
    [void]$adapter.Fill($data)

    if ($data.Rows.Count -eq 0) {
        Write-Output "Skipped [$SchemaName].[$TableName] (0 rows)"
        return
    }

    $bulkCopy = New-Object System.Data.SqlClient.SqlBulkCopy(
        $Destination,
        [System.Data.SqlClient.SqlBulkCopyOptions]::KeepIdentity,
        $null
    )
    $bulkCopy.DestinationTableName = "[$SchemaName].[$TableName]"
    $bulkCopy.BulkCopyTimeout = 600
    $bulkCopy.BatchSize = 500

    foreach ($column in $data.Columns) {
        [void]$bulkCopy.ColumnMappings.Add($column.ColumnName, $column.ColumnName)
    }

    $bulkCopy.WriteToServer($data)
    Write-Output "Copied [$SchemaName].[$TableName] => $($data.Rows.Count) rows"
}

$sourceConnection = $null
$destinationConnection = $null

try {
    $sourceConnection = Open-SqlConnection -ConnectionString $SourceConnectionString
    Write-Output "Connected to source database."

    $destinationConnection = Open-SqlConnection -ConnectionString $DestinationConnectionString
    Write-Output "Connected to destination database."

    $tables = Get-TableList -Connection $SourceConnection
    if ($tables.Rows.Count -eq 0) {
        throw "No user tables found in source database."
    }

    Disable-Constraints -Connection $destinationConnection
    Clear-DestinationTables -Connection $destinationConnection -Tables $tables

    foreach ($row in $tables.Rows) {
        Copy-TableData `
            -Source $sourceConnection `
            -Destination $destinationConnection `
            -SchemaName $row["SchemaName"] `
            -TableName $row["TableName"]
    }

    Enable-Constraints -Connection $destinationConnection
    Write-Output "Database sync completed successfully."
}
catch {
    Write-Error "Database sync failed: $($_.Exception.Message)"
    throw
}
finally {
    if ($sourceConnection -ne $null -and $sourceConnection.State -eq [System.Data.ConnectionState]::Open) {
        $sourceConnection.Close()
    }
    if ($destinationConnection -ne $null -and $destinationConnection.State -eq [System.Data.ConnectionState]::Open) {
        $destinationConnection.Close()
    }
}
