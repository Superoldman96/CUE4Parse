﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Landscape;
using CUE4Parse_Conversion.Materials;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.PoseAsset;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.UEFormat.Enums;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Nanite;

namespace CUE4Parse_Conversion
{
    public struct ExporterOptions
    {
        public ELodFormat LodFormat;
        public EMeshFormat MeshFormat;
        public ENaniteMeshFormat NaniteMeshFormat;
        public EAnimFormat AnimFormat;
        public EPoseFormat PoseFormat;
        public EMaterialFormat MaterialFormat;
        public ETextureFormat TextureFormat;
        public EFileCompressionFormat CompressionFormat;
        public ETexturePlatform Platform;
        public ESocketFormat SocketFormat;
        public bool ExportMorphTargets;
        public bool ExportMaterials;
        public bool ExportHdrTexturesAsHdr;

        public ExporterOptions()
        {
            LodFormat = ELodFormat.FirstLod;
            MeshFormat = EMeshFormat.ActorX;
            NaniteMeshFormat = ENaniteMeshFormat.OnlyNaniteLOD;
            AnimFormat = EAnimFormat.ActorX;
            MaterialFormat = EMaterialFormat.AllLayersNoRef;
            TextureFormat = ETextureFormat.Png;
            CompressionFormat = EFileCompressionFormat.None;
            Platform = ETexturePlatform.DesktopMobile;
            SocketFormat = ESocketFormat.Bone;
            ExportMorphTargets = true;
            ExportMaterials = true;
            ExportHdrTexturesAsHdr = true;
        }
    }

    public interface IExporter
    {
        public bool TryWriteToDir(DirectoryInfo directoryInfo, out string label, out string savedFileName);
        public bool TryWriteToZip(out byte[] zipFile);
        public void AppendToZip();
    }

    public abstract class ExporterBase : IExporter
    {
        protected readonly string PackagePath;
        protected readonly string ExportName;
        public ExporterOptions Options;

        protected ExporterBase()
        {
            PackagePath = string.Empty;
            ExportName = string.Empty;
            Options = new ExporterOptions();
        }

        protected ExporterBase(UObject export, ExporterOptions options)
        {
            var p = export.GetPathName();
            PackagePath = export.Owner?.Name ?? p.SubstringBeforeLast("."); // hm? (export.Owner?.Provider?.FixPath(p) ?? p).SubstringBeforeLast('.');
            ExportName = p.SubstringAfterLast('.');
            Options = options;
        }

        public abstract bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath);
        public abstract bool TryWriteToZip(out byte[] zipFile);
        public abstract void AppendToZip();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected string GetExportSavePath()
        {
            return GetExportSavePath(PackagePath, ExportName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetExportSavePath(string packagePath, string exportName)
        {
            var path = packagePath.SubstringAfterLast('/').Equals(exportName, StringComparison.InvariantCulture) ? packagePath : packagePath + '/' + exportName;
            return path[0] == '/' ? path[1..] : path;
        }

        protected string FixAndCreatePath(DirectoryInfo baseDirectory, string fullPath, string? ext = null)
        {
            if (fullPath.StartsWith('/')) fullPath = fullPath[1..];
            var ret = Path.Combine(baseDirectory.FullName, fullPath) + (ext != null ? $".{ext.ToLower()}" : "");
            Directory.CreateDirectory(ret.Replace('\\', '/').SubstringBeforeLast('/'));
            return ret;
        }
    }

    public class Exporter
    {
        private readonly ExporterBase _exporterBase;

        public Exporter(UObject export) : this(export, new ExporterOptions()) { }
        public Exporter(UObject export, ExporterOptions options)
        {
            _exporterBase = export switch
            {
                UAnimSequence animSequence => new AnimExporter(animSequence, options),
                UAnimMontage animMontage => new AnimExporter(animMontage, options),
                UAnimComposite animComposite => new AnimExporter(animComposite, options),
                UMaterialInterface material => new MaterialExporter2(material, options),
                USkeletalMesh skeletalMesh => new MeshExporter(skeletalMesh, options),
                USkeleton skeleton => new MeshExporter(skeleton, options),
                UStaticMesh staticMesh => new MeshExporter(staticMesh, options),
                ALandscapeProxy landscape => new LandscapeExporter(landscape, null, options),
                _ => throw new NotSupportedException($"export of '{export.GetType()}' is not supported yet.")
            };
        }

        public bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath) =>
            _exporterBase.TryWriteToDir(baseDirectory, out label, out savedFilePath);

        public bool TryWriteToZip(out byte[] zipFile) => _exporterBase.TryWriteToZip(out zipFile);

        public void AppendToZip() => _exporterBase.AppendToZip();
    }
}
