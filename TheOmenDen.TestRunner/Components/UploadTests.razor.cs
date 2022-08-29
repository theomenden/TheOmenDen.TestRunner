using Blazorise;

namespace TheOmenDen.TestRunner.Components;
public partial class UploadTests : ComponentBase
{
    private const int MegaByte = 1_048_576;

    private FileEdit _fileEdit;

    private int _uploadedFiles = 0;

    private readonly RunSummary _summary = new();

    [Inject] public ILogger<UploadTests> Logger { get; init; }

    [Inject] public ITestContextService TestContextService { get; init; }

    [Inject] public IPageProgressService PageProgressService { get; init; }

    protected override Task OnInitializedAsync()
    {
        PageProgressService.Go(0);
        return Task.CompletedTask;
    }
    
    private async Task OnChanged(FileChangedEventArgs e)
    {
        var fileName = String.Empty;

        try
        {
            _uploadedFiles = e.Files.Length;

            foreach (var file in e.Files)
            {
                fileName = file.Name;

                var buffer = new byte[MegaByte];

                await using var bufferedStream = new BufferedStream(file.OpenReadStream(long.MaxValue), MegaByte);
                
                int readBytes, readCount = 0;
                
                while ((readBytes = await bufferedStream.ReadAsync(buffer.AsMemory(0, MegaByte))) > 0)
                {
                    Console.WriteLine($"Read:{readCount++} {readBytes / (double)MegaByte} MB");
                    // Do work on the first 1MB of data
                    
                    _summary.Total++;
                }
            }
        }
        catch (Exception exc)
        {
            Logger.LogError("There was an issue when reading from the {FileName} file {@Exception}", fileName, exc);
        }
        finally
        {
            StateHasChanged();
        }
    }

    private Task OnWrittenAsync(FileWrittenEventArgs e)
    {
        Console.WriteLine($"File: {e.File.Name} Position: {e.Position} Data: {Convert.ToBase64String(e.Data)}");
        return Task.CompletedTask;
    }

    private Task OnProgressedAsync(FileProgressedEventArgs e)
    {
        PageProgressService.Go(Convert.ToInt32(e.Percentage), options => { options.Color = Color.Warning; });
        Console.WriteLine($"File: {e.File.Name} Progress: {e.Percentage}");
        return Task.CompletedTask;
    }

    private Task ResetFileInput()
    {
        _fileEdit.Reset().AsTask();

        return PageProgressService.Go(0);
    }

    private static async Task<String> DeserializeStreamToStringAsync(Stream? stream)
    {
        var content = String.Empty;

        if (stream is null)
        {
            return content;
        }

        using var sr = new StreamReader(stream);

        content = await sr.ReadToEndAsync();

        return content;
    }
}

