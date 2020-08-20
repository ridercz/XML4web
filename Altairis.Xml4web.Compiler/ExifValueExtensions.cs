using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace Altairis.Xml4web.Compiler {
    public static class ExifValueExtensions {

        // This class uses EXIF tag descriptions from
        // https://sno.phy.queensu.ca/~phil/exiftool/TagNames/EXIF.html

        public static string ToPrettyString(this IExifValue exif) {
            if (exif == null || exif.DataType == ExifDataType.Undefined || exif.DataType == ExifDataType.Unknown) return null;

            static string GetLabel(object value, string labels) {
                var labelArray = labels.Split(',').Select(s => s.Trim()).ToArray();
                var index = (ushort)value;
                return index < 0 || index >= labelArray.LongLength ? labelArray[0] : labelArray[index];
            }

            static IEnumerable<string> GetFlashFlags(object value) {
                var flags = (ushort)value;
                yield return (flags & 1) != 0 ? "Fired" : "Not fired";
                if ((flags & 2) != 0) yield return "Strobe return light detected";
                if ((flags & 4) != 0) yield return "Strobe return light not detected";
                if ((flags & 8) != 0) yield return "Compulsory flash mode";
                if ((flags & 16) != 0) yield return "Auto mode";
                if ((flags & 32) != 0) yield return "No flash function";
                if ((flags & 64) != 0) yield return "Red eye reduction mode";
            }

            static string GetGpsCoordinate(object value) {
                try {
                    var array = value as Rational[];
                    return $"{array[0]}°{array[1]}'{XmlConvert.ToString(array[2].ToDouble())}\"";
                } catch (Exception) {
                    return null;
                }
            }

            // Special cases
            if (exif.Tag == ExifTag.Orientation) return GetLabel(exif.GetValue(), "Unknown, Top Left, Top Right, Bottom Right, Bottom Left, Left Top, Right Top, Right Bottom, Left Bottom");
            if (exif.Tag == ExifTag.ExposureProgram) return GetLabel(exif.GetValue(), "Unknown, Manual, Auto, Aperture priority, Shutter speed priority, Creative, Action, Portrait, Landscape, Bulb");
            if (exif.Tag == ExifTag.MeteringMode) return GetLabel(exif.GetValue(), "Unknown, Average, Center Weighted Average, Spot, Multi Spot, Multi Segment, Partial");
            if (exif.Tag == ExifTag.LightSource) return GetLabel(exif.GetValue(), "Unknown, Daylight, Fluorescent, Tungsten, Flash, Sunny, Cloudy, Shade, Daylight Fluorescent, Day White Fluorescent, Cool White Fluorescent, White Fluorescent, Warm White Fluorescent, Standard Light A, Standard Light B, StandardLight C, D55, D65, D75, D50, ISO Studio Tungsten");
            if (exif.Tag == ExifTag.ExposureMode) return GetLabel(exif.GetValue(), "Auto, Manual, Auto bracket");
            if (exif.Tag == ExifTag.WhiteBalance) return GetLabel(exif.GetValue(), "Auto, Manual");
            if (exif.Tag == ExifTag.FNumber) return ((Rational)exif.GetValue()).ToDouble().ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
            if (exif.Tag == ExifTag.SceneCaptureType) return GetLabel(exif.GetValue(), "Standard, Landscape, Portrait, Night, Other");
            if (exif.Tag == ExifTag.DateTimeOriginal || exif.Tag == ExifTag.DateTimeDigitized) return DateTime.TryParseExact(exif.GetValue().ToString(), @"yyyy\:MM\:dd HH\:mm\:ss", null, System.Globalization.DateTimeStyles.None, out var dt) ? XmlConvert.ToString(dt, XmlDateTimeSerializationMode.RoundtripKind) : null;
            if (exif.Tag == ExifTag.Flash) return string.Join(", ", GetFlashFlags(exif.GetValue()));
            if (exif.Tag == ExifTag.GPSDestLatitude || exif.Tag == ExifTag.GPSDestLongitude || exif.Tag == ExifTag.GPSLatitude || exif.Tag == ExifTag.GPSLongitude) return GetGpsCoordinate(exif.GetValue());

            // Array of generic values
            if (exif.IsArray) {
                var s = string.Empty;
                foreach (var item in exif.GetValue() as Array) {
                    s += item.ToString() + " ";
                }
                return s.Trim();
            }

            // If no special case occurred, just return ToString()
            return exif.GetValue().ToString();
        }
    }
}
