using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;


public class RunResult
{
    public string ProgramName { get; set; } = "";
    public string SourceCode { get; set; } = "";
    public byte[]? CompiledBytecode { get; set; }
    public string? RunLog { get; set; } 
    public bool IsSuccess { get; set; }
    public string ErrorCategory { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public long DurationMs { get; set; }
}

public class DatabaseManager
{
    private readonly string _connectionString;
    public DatabaseManager()
    {
        string dbPath = "Database/MiniVM_Results.db";
        _connectionString = $"Data Source={dbPath}";
    }

    public void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();

        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS SuccessfulRuns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProgramName TEXT NOT NULL,
                SourceCode TEXT NOT NULL,
                CompiledBytecode BLOB,
                RunLog TEXT,
                DurationMs INTEGER,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );
        ";
        command.ExecuteNonQuery();

        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS FailedRuns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProgramName TEXT NOT NULL,
                SourceCode TEXT NOT NULL,
                ErrorCategory TEXT NOT NULL,
                ErrorMessage TEXT NOT NULL,
                DurationMs INTEGER,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );
        ";
        command.ExecuteNonQuery();
    }

    public void SaveSuccessfulResults(List<RunResult> results)
    {
        if (results.Count == 0) return;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO SuccessfulRuns (ProgramName, SourceCode, CompiledBytecode, RunLog, DurationMs)
            VALUES ($name, $source, $bytecode, $log, $duration);
        ";

        var pName = command.CreateParameter(); pName.ParameterName = "$name"; command.Parameters.Add(pName);
        var pSource = command.CreateParameter(); pSource.ParameterName = "$source"; command.Parameters.Add(pSource);
        var pBytecode = command.CreateParameter(); pBytecode.ParameterName = "$bytecode"; command.Parameters.Add(pBytecode);
        var pLog = command.CreateParameter(); pLog.ParameterName = "$log"; command.Parameters.Add(pLog);
        var pDuration = command.CreateParameter(); pDuration.ParameterName = "$duration"; command.Parameters.Add(pDuration);

        foreach (var res in results)
        {
            pName.Value = res.ProgramName;
            pSource.Value = res.SourceCode;
            pBytecode.Value = res.CompiledBytecode ?? (object)DBNull.Value;
            pLog.Value = res.RunLog ?? (object)DBNull.Value;
            pDuration.Value = res.DurationMs;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        Console.WriteLine($"{results.Count} db, Successful test.");
    }

    public void SaveFailedResults(List<RunResult> results)
    {
        if (results.Count == 0) return;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO FailedRuns (ProgramName, SourceCode, ErrorCategory, ErrorMessage, DurationMs)
            VALUES ($name, $source, $category, $message, $duration);
        ";

        var pName = command.CreateParameter(); pName.ParameterName = "$name"; command.Parameters.Add(pName);
        var pSource = command.CreateParameter(); pSource.ParameterName = "$source"; command.Parameters.Add(pSource);
        var pCat = command.CreateParameter(); pCat.ParameterName = "$category"; command.Parameters.Add(pCat);
        var pMsg = command.CreateParameter(); pMsg.ParameterName = "$message"; command.Parameters.Add(pMsg);
        var pDuration = command.CreateParameter(); pDuration.ParameterName = "$duration"; command.Parameters.Add(pDuration);

        foreach (var res in results)
        {
            pName.Value = res.ProgramName;
            pSource.Value = res.SourceCode;
            pCat.Value = res.ErrorCategory;
            pMsg.Value = res.ErrorMessage;
            pDuration.Value = res.DurationMs;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        Console.WriteLine($"{results.Count} db, Failed test.");
    }

    public void PrintErrorStatistics()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ErrorCategory, COUNT(*) as ErrorCount 
            FROM FailedRuns 
            GROUP BY ErrorCategory 
            ORDER BY ErrorCount DESC;
        ";

        using var reader = command.ExecuteReader();
        bool hasErrors = false;
        while (reader.Read())
        {
            hasErrors = true;
            Console.WriteLine($"- {reader.GetString(0)}: {reader.GetInt32(1)} db");
        }
        if (!hasErrors) Console.WriteLine("No failed test in the database");
    }
}
