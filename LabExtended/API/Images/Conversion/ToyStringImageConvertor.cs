﻿using LabExtended.Extensions;

using NorthwoodLib.Pools;

using LabExtended.Utilities;

using UnityEngine;

using Color = System.Drawing.Color;

namespace LabExtended.API.Images.Conversion;

/// <summary>
/// Converts an image to be used in text toys.
/// </summary>
public static class ToyStringImageConvertor
{
    /// <summary>
    /// Toy string formatting options.
    /// </summary>
    public class ToyStringImageData
    {
        /// <summary>
        /// The size of the pixel character.
        /// </summary>
        public int CharacterSize = 33;

        /// <summary>
        /// The line height between each line.
        /// </summary>
        public int CharacterHeight = 75;

        /// <summary>
        /// The text toy scale.
        /// </summary>
        public Vector3 Scale;

        /// <summary>
        /// The text toy display size.
        /// </summary>
        public Vector2 Display;
    }
    
    /// <summary>
    /// Gets or sets the character used to represent a pixel.
    /// </summary>
    public static char Character { get; set; } = '█';
    
    /// <summary>
    /// Assigns text toy data to each image frame.
    /// </summary>
    /// <param name="file">The target image file.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void ConvertImage(ImageFile file)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));
        
        var builder = StringBuilderPool.Shared.Rent();

        Color? last = null;
        
        for (var index = 0; index < file.Frames.Count; index++)
        {
            var frame = file.Frames[index];
            
            builder.Clear();
            
            builder.Append("<size=");
            builder.Append(file.toyStringImageData.CharacterSize);
            builder.Append("%><line-height=");
            builder.Append(file.toyStringImageData.CharacterHeight);
            builder.Append("%>");

            for (var x = 0; x < frame.Pixels.Count; x++)
            {
                var pixels = frame.Pixels[x];

                for (var y = 0; y < pixels.Count; y++)
                {
                    var pixel = pixels[y];

                    if (!last.HasValue || last.Value != pixel.Color)
                    {
                        builder.Append("<color=");
                        builder.Append(pixel.Color.ToShortHex());
                        builder.Append(">");

                        last = pixel.Color;
                    }

                    builder.Append(Character);
                }

                builder.AppendLine();
            }
            
            frame.toyFrameData ??= new();

            builder.Append("</color>");
            builder.ToString().SplitByLengthUtf8(MirrorMethods.MaxStringLength, frame.toyFrameData);

            builder.Clear();

            for (var x = 0; x < frame.toyFrameData.Count; x++)
            {
                builder.Append('{');
                builder.Append(x);
                builder.Append('}');
            }
            
            frame.toyFrameFormat = builder.ToString();
        }
        
        StringBuilderPool.Shared.Return(builder);
    }

    /// <summary>
    /// Reads custom image properties.
    /// </summary>
    /// <param name="target">The target image file.</param>
    /// <param name="reader">The binary reader.</param>
    public static void ReadImage(ImageFile target, BinaryReader reader)
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));

        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        target.toyStringImageData.CharacterSize = reader.ReadInt32();
        target.toyStringImageData.CharacterHeight = reader.ReadInt32();

        var scale = new Vector3();

        scale.x = reader.ReadSingle();
        scale.y = reader.ReadSingle();
        scale.z = reader.ReadSingle();

        target.toyStringImageData.Scale = scale;

        var display = new Vector2();

        display.x = reader.ReadSingle();
        display.y = reader.ReadSingle();

        target.toyStringImageData.Display = display;
    }

    /// <summary>
    /// Saves debug data for this image.
    /// </summary>
    /// <param name="file">The image to save debug data of.</param>
    /// <param name="outputPath">Path to the output file.</param>
    public static void DebugImage(ImageFile file, string outputPath)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));
        
        var builder = StringBuilderPool.Shared.Rent();

        builder.AppendLine("-- ToyStringImageConvertor Debug --");
        
        builder.AppendLine($"Character = {Character}");
        builder.AppendLine($"CharacterSize = {file.toyStringImageData.CharacterSize}");
        builder.AppendLine($"CharacterHeight = {file.toyStringImageData.CharacterHeight}");

        builder.AppendLine($"Name = {file.Name} ({file.Extension})");
        builder.AppendLine($"DisplaySize = {file.toyStringImageData.Display.ToPreciseString()}");
        builder.AppendLine($"FrameCount = {file.Frames.Count}");
        builder.AppendLine($"IsAnimated = {file.IsAnimated}");

        builder.AppendLine("-- Frame Debug --");

        for (var i = 0; i < file.Frames.Count; i++)
        {
            var frame = file.Frames[i];

            builder.AppendLine();
            builder.AppendLine($"== Frame {i} ==");

            builder.AppendLine("-- Format --");
            builder.AppendLine(frame.toyFrameFormat);
            
            builder.AppendLine();
            builder.AppendLine("-- Pixels --");

            for (var x = 0; x < frame.toyFrameData.Count; x++)
            {
                builder.AppendLine($"[{x}] {frame.toyFrameData[x]}");
            }
        }
        
        File.WriteAllText(outputPath, StringBuilderPool.Shared.ToStringReturn(builder));
    }
}