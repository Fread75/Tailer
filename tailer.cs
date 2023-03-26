using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using NLog;

public class Tailer
{
    public delegate void LineHandler(List<string> lines);

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly string _filePath;
    private readonly LineHandler _lineHandler;
    private readonly bool _isCompressed;
    private long _lastReadPosition;

    public Tailer(string filePath, LineHandler lineHandler)
    {
        _filePath = filePath;
        _lineHandler = lineHandler;
        _isCompressed = Path.GetExtension(filePath).Equals(".gz", StringComparison.OrdinalIgnoreCase);
    }

    public void Read()
    {
        Logger.Trace("Lecture du fichier...");

        if (!File.Exists(_filePath))
        {
            Logger.Error($"Le fichier '{_filePath}' n'existe pas.");
            return;
        }

        try
        {
            using (var reader = GetReader(_filePath, _isCompressed))
            {
                ProcessNewLines(ref _lastReadPosition, reader);
            }
        }
        catch (IOException ex)
        {
            Logger.Error($"Erreur lors de la lecture du fichier : {ex.Message}");
        }
    }

    private StreamReader GetReader(string filePath, bool isCompressed)
    {
        FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return isCompressed
            ? new StreamReader(new GZipStream(fileStream, CompressionMode.Decompress), Encoding.UTF8)
            : new StreamReader(fileStream, Encoding.UTF8);
    }

    private void ProcessNewLines(ref long lastReadPosition, StreamReader reader)
    {
        if (reader.BaseStream.Length < lastReadPosition)
        {
            Logger.Trace("Le fichier a été tronqué, lecture depuis le début...");
            lastReadPosition = 0;
        }

        reader.BaseStream.Position = lastReadPosition;

        var newLines = new List<string>();
        string line;
        long currentPosition = lastReadPosition;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.EndsWith("\n") || reader.Peek() == -1)
            {
                newLines.Add(line.TrimEnd('\n'));
                currentPosition = reader.BaseStream.Position;
            }
            else
            {
                break;
            }
        }

        if (newLines.Count > 0)
        {
            _lineHandler(newLines);
        }

        lastReadPosition = currentPosition;
    }
}

public class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Logger.Error("Veuillez fournir un chemin de fichier.");
            return;
        }

        string filePath = args[0];
        var tailer = new Tailer(filePath, ProcessLines);

        using (var timer = new Timer(_ => tailer.Read(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)))
        {
            Logger.Info("Appuyez sur une touche pour quitter...");
            Console.ReadKey();
        }
    }

    public static void ProcessLines(List<string> lines)
    {
        foreach (var line in lines)
        {
            Logger.Trace($"Nouvelle ligne : {line}");
        }
    }
}
