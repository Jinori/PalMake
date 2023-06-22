using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagick;

namespace PalMake;

public class FileProcessingService
{
    public async Task WorkerOnDoWork(IReadOnlyList<string> paths, MainWindow mainWindow)
    {
        string[] filesToDelete = { "gui06.bat", "gui06.epf", "gui06.tbl" };
        var inputPath = paths[0];
        var outputPath = paths[1];

        mainWindow.Dispatcher.Invoke(() => { mainWindow.MyLabelText = $"Starting process..."; });
        if (!Directory.Exists(inputPath) || !Directory.Exists(outputPath))
        {
            mainWindow.Dispatcher.Invoke(() => { mainWindow.MyLabelText = $"A directory does not exist."; });
            return;
        }

        await Task.Run(() =>
        {
            GenerateDuotoneImages("skill_", "skillo_", inputPath, inputPath, MagickColors.Aqua, 35, false, mainWindow);
        });
        await Task.Run(() =>
        {
            GenerateDuotoneImages("skill_", "skillu_", inputPath, inputPath, MagickColors.Gray, 60, true, mainWindow);
        });
        await Task.Run(() =>
        {
            GenerateDuotoneImages("spell_", "spello_", inputPath, inputPath, MagickColors.Aqua, 35, false, mainWindow);
        });
        await Task.Run(() =>
        {
            GenerateDuotoneImages("spell_", "spellu_", inputPath, inputPath, MagickColors.Gray, 60, true, mainWindow);
        });
        var fileNames = Directory.GetFiles(inputPath, "*png");
        ICollection<string> images = new List<string>(fileNames);
        mainWindow.Dispatcher.Invoke(() =>
        {
            mainWindow.MyLabelText = $"Quantizing files to adhere to same palette...";
        });
        await Task.Run(() =>
        {
            Quantize(images, outputPath);
        });
        // 2nd Step: Create gui06.pal from those images
        var imagesToGenerate = Directory.GetFiles(outputPath)
            .Where(f => Path.GetExtension(f) == ".png")
            .ToList();

        // Sort the images by their numerical part
        imagesToGenerate.Sort((a, b) =>
            int.Parse(Path.GetFileNameWithoutExtension(a).Split('_')[1].Substring(1)) -
            int.Parse(Path.GetFileNameWithoutExtension(b).Split('_')[1].Substring(1)));

        string[] filesToCopy =
            { "bmp2epf.exe", "msvcp140d.dll", "ucrtbased.dll", "vcruntime140.dll", "vcruntime140d.dll" };
        const string sourcePath = @"tools\"; // Relative path to the 'tools' subdirectory
        mainWindow.Dispatcher.Invoke(() => { mainWindow.MyLabelText = $"Copying tools to output directory..."; });
        foreach (var fileName in filesToCopy)
        {
            var sourceFilePath = Path.Combine(sourcePath, fileName);
            var targetFilePath = Path.Combine(outputPath, fileName);

            try
            {
                File.Copy(sourceFilePath, targetFilePath, true);
            }
            catch (Exception ex)
            {
                // Handle the exception or log the error message
                Console.WriteLine($"Failed to copy file: {sourceFilePath} to {targetFilePath}");
                Console.WriteLine(ex.Message);
            }
        }

        mainWindow.Dispatcher.Invoke(() => { mainWindow.MyLabelText = $"Generating Gui06 Pal..."; });
        // Generate the command string
        var command = string.Join(" ", imagesToGenerate.Select(current => $"--frame {Path.GetFileName(current)}"));
        command += $" --outfilename gui06.epf";
        
        await RunSkillSpellCommandAsync(command, outputPath);

        // Generate other files
        await CreateFiles(outputPath, mainWindow);


        mainWindow.Dispatcher.Invoke(() => { mainWindow.MyLabelText = $"Cleaning up files...."; });
        foreach (var fileName in filesToCopy.Concat(filesToDelete))
        {
            File.Delete(Path.Combine(outputPath, fileName));
        }

        foreach (var fileName in imagesToGenerate)
        {
            File.Delete(Path.Combine(outputPath, fileName));
        }

        mainWindow.Dispatcher.Invoke(() => { mainWindow.MyLabelText = $"Done!"; });
    }

    private void Quantize(ICollection<string> inputpaths, string outputPath)
    {
        using var imageCollection = new MagickImageCollection();

        foreach (var path in inputpaths)
        {
            Console.WriteLine(path); // Print the input path for debugging
            imageCollection.Add(path);
        }


        imageCollection.Quantize(new QuantizeSettings
        {
            Colors = 256,
            ColorSpace = ColorSpace.sRGB
        });

        foreach (var tuple in imageCollection.Zip(inputpaths))
        {
            var filename = Path.GetFileName(tuple.Second);
            tuple.First.Write(Path.Combine(outputPath, filename)); // Note the change here
        }
    }

    private void GenerateDuotoneImages(string sourcePrefix, string targetPrefix, string inputPath, string outputPath,
        MagickColor color, double alphaPercentage, bool convertToGrayscale, MainWindow mainWindow)
    {
        mainWindow.Dispatcher.Invoke(() => { mainWindow.MyLabelText = $"Generating {sourcePrefix} files..."; });
        var sourceFiles = Directory.GetFiles(inputPath, $"{sourcePrefix}*.png");
        foreach (var sourceFile in sourceFiles)
        {
            var fileName = Path.GetFileName(sourceFile);
            var fileNameWithoutPrefix = fileName.Substring(sourcePrefix.Length);
            var targetFile = Path.Combine(outputPath, $"{targetPrefix}{fileNameWithoutPrefix}");

            // Generate duotone image
            using var image = new MagickImage(sourceFile);
            if (fileName.StartsWith(sourcePrefix))
            {
                // Adjust the color and alpha of the image
                image.Colorize(color, new Percentage(alphaPercentage));
            }

            if (convertToGrayscale)
            {
                // Convert the image to grayscale
                image.Grayscale(PixelIntensityMethod.Average);
            }

            // Save the target image
            image.Write(targetFile);
        }
    }

    private async Task CreateFiles(string outdir, MainWindow mainWindow)
    {
        var prefixes = new List<string>
        {
            "skill_F",
            "skillo_F",
            "skillu_F",
            "spell_F",
            "spello_F",
            "spellu_F",
        };

        var outFiles = new List<string>
        {
            "skill001.epf",
            "skill002.epf",
            "skill003.epf",
            "spell001.epf",
            "spell002.epf",
            "spell003.epf",
        };

        for (var i = 0; i < prefixes.Count; i++)
        {
            var i1 = i;
            mainWindow.Dispatcher.Invoke(() => { mainWindow.MyLabelText = $"Generating {prefixes[i1]} files..."; });
            await CreateFile(prefixes[i], outFiles[i], outdir);
        }
    }

    private async Task CreateFile(string prefix, string outfile, string outdir)
    {
        var imageFolder = outdir;

        var images = Directory.GetFiles(imageFolder)
            .Select(Path.GetFileName)
            .Where(f => f != null && f.StartsWith(prefix) && f.EndsWith(".png"))
            .ToList();

        images.Sort((a, b) =>
            int.Parse(Path.GetFileNameWithoutExtension(a)!.Split('_')[1].Substring(1)) -
            int.Parse(Path.GetFileNameWithoutExtension(b)!.Split('_')[1].Substring(1)));

        List<string> currentCommandImages = images
            .Select(image => $" --frame {image}")
            .ToList();

        string command = String.Join("", currentCommandImages) + $" --pallete gui06.pal --outfilename {outfile}";

        await RunSkillSpellCommandAsync(command, imageFolder);
    }

    private async Task RunSkillSpellCommandAsync(string command, string workingDirectory)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(workingDirectory, "bmp2epf.exe"),
                Arguments = command,
                WorkingDirectory = workingDirectory
            },
            EnableRaisingEvents = true
        };
        process.Exited += (_, _) => source.TrySetResult();
        process.Start();
        await source.Task;
    }
}