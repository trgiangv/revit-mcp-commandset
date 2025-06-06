﻿// 
//                       RevitAPI-Solutions
// Copyright (c) Duong Tran Quang (DTDucas) (baymax.contact@gmail.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//

using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Models.Architecture;

/// <summary>
///     Information about a door
/// </summary>
public class DoorInfo
{
    /// <summary>
    ///     Default constructor
    /// </summary>
    public DoorInfo()
    {
        Location = new JZPoint(0, 0, 0);
    }

    /// <summary>
    ///     Constructor with position and dimensions
    /// </summary>
    /// <param name="x">X position (mm)</param>
    /// <param name="y">Y position (mm)</param>
    /// <param name="z">Z position (mm)</param>
    /// <param name="width">Width (mm)</param>
    /// <param name="height">Height (mm)</param>
    /// <param name="level">Floor elevation (mm)</param>
    public DoorInfo(double x, double y, double z, double width, double height, double level = 0)
    {
        Location = new JZPoint(x, y, z);
        Width = width;
        Height = height;
        Level = level;
    }

    /// <summary>
    ///     Door position (mm)
    /// </summary>
    [JsonProperty("location")]
    public JZPoint Location { get; set; }

    /// <summary>
    ///     Host wall for the door (reference ID)
    /// </summary>
    [JsonProperty("hostWallId")]
    public string HostWallId { get; set; }

    /// <summary>
    ///     Door width (mm)
    /// </summary>
    [JsonProperty("width")]
    public double Width { get; set; }

    /// <summary>
    ///     Door height (mm)
    /// </summary>
    [JsonProperty("height")]
    public double Height { get; set; }

    /// <summary>
    ///     Height from floor (mm)
    /// </summary>
    [JsonProperty("elevation")]
    public double Elevation { get; set; }

    /// <summary>
    ///     Host floor elevation (mm)
    /// </summary>
    [JsonProperty("level")]
    public double Level { get; set; }

    /// <summary>
    ///     Door type
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } // Single, Double, Sliding, etc.

    /// <summary>
    ///     Door type ID in Revit
    /// </summary>
    [JsonProperty("typeId")]
    public int TypeId { get; set; }

    /// <summary>
    ///     Door material
    /// </summary>
    [JsonProperty("material")]
    public string Material { get; set; }

    /// <summary>
    ///     Door opening angle (degrees)
    /// </summary>
    [JsonProperty("openingAngle")]
    public double OpeningAngle { get; set; }

    /// <summary>
    ///     Additional options
    /// </summary>
    [JsonProperty("options")]
    public Dictionary<string, object> Options { get; set; } = new();
}