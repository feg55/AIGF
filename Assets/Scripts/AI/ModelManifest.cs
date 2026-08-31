using System;
using UnityEngine;

namespace Aigf.Companion.AI
{
    public sealed class ModelManifest
    {
        [Serializable]
        private sealed class ManifestDto
        {
            public string id;
            public string filename;
            public int version;
            public string sha256;
            public long sizeBytes;
            public int contextSize;
            public int maxTokens;
            public string quantization;
        }

        public string Id { get; private set; }
        public string Filename { get; private set; }
        public int Version { get; private set; }
        public string Sha256 { get; private set; }
        public long SizeBytes { get; private set; }
        public int ContextSize { get; private set; }
        public int MaxTokens { get; private set; }
        public string Quantization { get; private set; }

        public static bool TryParse(string json, out ModelManifest manifest, out string error)
        {
            manifest = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Model manifest is empty.";
                return false;
            }

            ManifestDto dto;
            try
            {
                dto = JsonUtility.FromJson<ManifestDto>(json);
            }
            catch (Exception exception)
            {
                error = $"Model manifest JSON is malformed: {exception.Message}";
                return false;
            }

            if (dto == null || string.IsNullOrWhiteSpace(dto.id) || !IsSafeFilename(dto.filename))
            {
                error = "Model manifest requires an ID and a project-local .gguf filename.";
                return false;
            }

            if (dto.version < 1 || dto.contextSize < 512 || dto.maxTokens < 1)
            {
                error = "Model manifest contains invalid version or inference limits.";
                return false;
            }

            var hash = (dto.sha256 ?? string.Empty).Trim().ToLowerInvariant();
            if (hash.Length != 0 && (hash.Length != 64 || !IsHex(hash)))
            {
                error = "Model manifest SHA-256 must be empty or 64 hexadecimal characters.";
                return false;
            }

            manifest = new ModelManifest
            {
                Id = dto.id.Trim(),
                Filename = dto.filename.Trim(),
                Version = dto.version,
                Sha256 = hash,
                SizeBytes = Math.Max(0L, dto.sizeBytes),
                ContextSize = dto.contextSize,
                MaxTokens = dto.maxTokens,
                Quantization = (dto.quantization ?? string.Empty).Trim()
            };
            return true;
        }

        public string ToJson(bool pretty = false)
        {
            return JsonUtility.ToJson(new ManifestDto
            {
                id = Id,
                filename = Filename,
                version = Version,
                sha256 = Sha256,
                sizeBytes = SizeBytes,
                contextSize = ContextSize,
                maxTokens = MaxTokens,
                quantization = Quantization
            }, pretty);
        }

        private static bool IsSafeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) ||
                !filename.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
                filename.IndexOf('/') >= 0 || filename.IndexOf('\\') >= 0 || filename.Contains(".."))
            {
                return false;
            }

            return true;
        }

        private static bool IsHex(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
