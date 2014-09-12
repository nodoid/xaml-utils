using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace xib2xaml
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage : xib2xaml <infile>.xib <outfile>.xaml");
                Environment.Exit(-1);
            }

            var convert = new Converter(args[0], args[1]);
            //var convert = new Converter("AnimalTypes_iPhone.xib", "test.xaml");
            convert.ConvertFile();
        }
    }

    class UIObject
    {
        public string UIName { get; set; }

        public string UIElement { get; set; }

        public string Text { get; set; }

        public string FontSize { get; set; }

        public string UIWidth { get; set; }

        public string UIHeight { get; set; }

        public string UIXPos { get; set; }

        public string UIYPos { get; set; }

        public string BackgroundColor { get; set; }

        public string TextColor { get; set; }

        public string OtherDetails { get; set; }

        public string ColorA { get; set; }

        public string ColorB { get; set; }

        public string ColorG { get; set; }

        public string ColorR { get; set; }

        public string ColorW { get; set; }

        public string TextHAlign { get; set; }

        public string TextVAlign { get; set; }
    }


    class Converter
    {
        string infile, outfile;

        public Converter(string inf, string outf)
        {
            infile = inf;
            outfile = outf;
        }

        public async void ConvertFile()
        {
            if (File.Exists(outfile))
            {
                Console.WriteLine("{0} already exists");
                return;
            }
            else
                CreateHeader();

            /* parse logic
             * 1. Find the views, count - each needs a new file
             * 2. For each view, parse for each destination id
             * 3. Create UIObject for each destination id object
             * 4. Output to file
             * 5. Rinse and repeat */

            int NumOfViews = 0;
            var ViewIDs = new List<string>();
            var Outlets = new Dictionary<string, string>();
            using (var reader = File.OpenText(infile))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line.Contains("UIView"))
                    {
                        NumOfViews++;
                        var destList = line.Split('"');
                        ViewIDs.Add(destList[2].Substring(line.IndexOf(destList[2]), line.IndexOf(destList[3])));
                    }
                    else
                    {
                        if (line.Contains("outlet"))
                        {
                            var doc = XDocument.Parse(line);
                                var outKey = doc.Root.Element("outlet").Attribute("property").Value;
                                var outVal = doc.Root.Element("outlet").Attribute("destination").Value;
                                Outlets.Add(outKey, outVal);
                        }
                    }
                }
            }

            if (NumOfViews == 0)
            {
                foreach (var search in Outlets.Values)
                {
                    var ui = new UIObject();
                    var fullSearch = string.Format("id=\"{0}\"", search);
                    string stringBlock = "", element = "";
                    using (var reader = File.OpenText(infile))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = await reader.ReadLineAsync();
                            if (line.Contains(fullSearch))
                            {
                                element = line.Substring(0, line.IndexOf(" "));
                                var endElement = string.Format("</{0}>", element.Substring(1));
                                while (!line.Contains(endElement))
                                    stringBlock += line;
                            }
                            ui.UIName = Outlets.FirstOrDefault(t => t.Value == search).Key;
                            if (ui.UIName != "view")
                            {
                                ui.UIElement = element;
                                var doc = XDocument.Parse(stringBlock);
                                ui.UIXPos = doc.Root.Element("rect").Attribute("x").Value;
                                ui.UIYPos = doc.Root.Element("rect").Attribute("y").Value;
                                ui.UIWidth = doc.Root.Element("rect").Attribute("width").Value;
                                ui.UIHeight = doc.Root.Element("rect").Attribute("height").Value;
                                ui.Text = doc.Root.Element(element).Attribute("text").Value;
                                if (string.IsNullOrEmpty(ui.Text))
                                    ui.Text = doc.Root.Element(element).Attribute("title").Value;
                                ui.FontSize = doc.Root.Element("fontDescription").Attribute("pointSize").Value;
                                ui.TextColor = doc.Root.Element("color").Attribute("cocoaTouchSystemColor").Value;
                                if (string.IsNullOrEmpty(ui.TextColor))
                                {
                                    ui.ColorA = doc.Root.Element("color").Attribute("alpha").Value;
                                    ui.ColorB = doc.Root.Element("color").Attribute("blue").Value;
                                    ui.ColorG = doc.Root.Element("color").Attribute("green").Value;
                                    ui.ColorR = doc.Root.Element("red").Attribute("red").Value;
                                    ui.ColorW = doc.Root.Element("white").Attribute("white").Value;
                                }
                                ui.OtherDetails = doc.Root.Element("imageView").Attribute("image").Value;
                                ui.TextHAlign = doc.Root.Element(element).Attribute("contentHorizontalAlignment").Value;
                                ui.TextVAlign = doc.Root.Element(element).Attribute("contentVerticalAlignment").Value;
                            }
                            else
                            {

                            }
                            OutputUIElement(ui);
                            ui = null;
                        }

                    }
                }
            }
            else
            {

            }
            CreateFooter();
        }

        private void CreateHeader()
        {
            using (var writer = File.CreateText(outfile))
            {
                writer.WriteLineAsync("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                writer.WriteLineAsync("<ContentPage xmlns=\"http://xamarin.com/schemas/2014/forms\"");
                writer.WriteLineAsync("xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
                writer.WriteLineAsync("x:Class=\"HelloXamarinFormsWorldXaml.AbsoluteLayoutExample\"");
                writer.WriteLineAsync("Padding=\"20\">");
                writer.WriteLineAsync();
                writer.WriteLineAsync("<AbsoluteLayout>");
                writer.WriteLineAsync();
            }
        }

        private void CreateFooter()
        {
            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLineAsync("</AbsoluteLayout>");
                writer.WriteLineAsync("</ContentPage>");
            }
        }

        private void OutputUIElement(UIObject ui)
        {
            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLineAsync(string.Format("<{0}", ui.UIElement));
                if (!string.IsNullOrEmpty(ui.UIName))
                    writer.WriteLineAsync(string.Format("x:Name=\"{0}\"", ui.UIName));
                if (!string.IsNullOrEmpty(ui.Text))
                    writer.WriteLineAsync(string.Format("Text=\"{0}\"", ui.Text));
                if (!string.IsNullOrEmpty(ui.FontSize))
                    writer.WriteLineAsync(string.Format("Font=\"{0}\"", ui.FontSize));

                writer.WriteLineAsync(string.Format("AbsoluteLayout.LayoutBounds=\"{0},{1},{2},{3}\"", ui.UIXPos, ui.UIYPos, ui.UIWidth, ui.UIHeight));

                if (!string.IsNullOrEmpty(ui.BackgroundColor))
                    writer.WriteLineAsync(string.Format("BackgroundColor=\"{0}\"", ui.BackgroundColor));
                if (!string.IsNullOrEmpty(ui.TextColor))
                    writer.WriteLineAsync(string.Format("TextColor=\"{0}\"", ui.TextColor));
                else
                {
                    if (string.IsNullOrEmpty(ui.ColorA))
                    {
                        int r = (int)(255 * double.Parse(ui.ColorR));
                        int g = (int)(255 * double.Parse(ui.ColorG));
                        int b = (int)(255 * double.Parse(ui.ColorB));
                        writer.WriteLineAsync(string.Format("TextColor=\"#{0}{1}{2}\"", r.ToString("X"), g.ToString("X"), b.ToString("X")));
                    }
                    else
                    {
                        int a = (int)(255 * double.Parse(ui.ColorA));
                        int r = (int)(255 * double.Parse(ui.ColorR));
                        int g = (int)(255 * double.Parse(ui.ColorG));
                        int b = (int)(255 * double.Parse(ui.ColorB));
                        writer.WriteLineAsync(string.Format("TextColor=\"#{0}{1}{2}{3}\"", a.ToString("X"), r.ToString("X"), g.ToString("X"), b.ToString("X")));
                    }
                        
                }
                if (!string.IsNullOrEmpty(ui.OtherDetails))
                    writer.WriteLineAsync(ui.OtherDetails);
                writer.WriteLineAsync(" />");
                writer.WriteLineAsync();
            }
        }
    }
}
