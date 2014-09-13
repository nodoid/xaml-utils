using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml;
using System.Configuration;

namespace xib2xaml
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            /* if (args.Length != 2)
            {
                Console.WriteLine("Usage : xib2xaml <infile>.xib <outfile>.xaml");
                Environment.Exit(-1);
            }

            var convert = new Converter(args[0], args[1]);*/
            var convert = new Converter("AnimalTypes_iPhone.xib", "test25.xaml");
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
                File.Delete(outfile);
                CreateHeader();
                //Console.WriteLine("{0} already exists", outfile);
                //return;
            }
            else
                CreateHeader();

            try
            {
                int NumOfViews = 0;
                var ViewIDs = new List<string>();
                var Outlets = new Dictionary<string, string>();
                string connections = "";
                using (var reader = File.OpenText(infile))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (line.Contains("connections"))
                        {
                            var nl = reader.ReadLine();
                            while (!nl.Contains("connections"))
                            {
                                connections += nl + ":";
                                nl = reader.ReadLine();
                            }
                        }
                    }
                }

                connections = connections.TrimEnd(':');

                var connect = connections.Split(':').ToList();
                foreach (var cnt in connect)
                {
                    if (cnt.Contains("UIView"))
                    {
                        NumOfViews++;
                        var doc = XDocument.Parse(cnt);
                        ViewIDs.Add(doc.Root.Attribute("destination").Value);
                    }
                    else
                    {
                        var doc = XDocument.Parse(cnt);
                        var outKey = doc.Root.Attribute("property").Value;
                        var outVal = doc.Root.Attribute("destination").Value;
                        Outlets.Add(outKey, outVal);
                    }
                }

                if (NumOfViews == 0)
                {
                    foreach (var search in Outlets.Values)
                    {
                        string node = "";

                        using (var reader = File.OpenText(infile))
                        {
                            while (!reader.EndOfStream)
                            {
                                var fullSearch = string.Format("id=\"{0}\"", search);
                                string stringBlock = "", element = "";         
                                var line = reader.ReadLine();
                                if (line.Contains(fullSearch))
                                {
                                    var ui = new UIObject();
                                    var tt = line.Split('<');
                                    element = tt[1].Split(' ')[0];
                                    node += line + "\n";
                                    var nl = reader.ReadLine();
                                    while (!nl.Contains(element))
                                    {
                                        node += nl + "\n";
                                        nl = reader.ReadLine();
                                    }
                                    node += "</" + element + ">";

                                    var ts = fullSearch.Split('"');
                                    try
                                    {
                                        ui.UIName = Outlets.SingleOrDefault(t => t.Value == ts[1]).Key;
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Cant find key for {0} : {1}-{2}", ts[1], ex.Message, ex.StackTrace);
                                    }
                                    if (ui.UIName != "view")
                                    {
                                        var doc = XDocument.Parse(node);
                                        XNode nodet = null;
                                        XDocument nDoc = null;
                                        if (doc.ToString().Contains("state"))
                                        {
                                            nodet = doc.Root.Element("state").FirstNode;
                                            nDoc = XDocument.Parse(nodet.ToString());
                                        }

                                        var docTS = doc.ToString();

                                        ui.UIElement = doc.Root.Name.LocalName;
                                        
                                        ui.UIXPos = doc.Root.Element("rect").Attribute("x").Value;
                                        ui.UIYPos = doc.Root.Element("rect").Attribute("y").Value;
                                        ui.UIWidth = doc.Root.Element("rect").Attribute("width").Value;
                                        ui.UIHeight = doc.Root.Element("rect").Attribute("height").Value;
                                        if (element == "text")
                                            ui.Text = doc.Root.Element("label").Attribute("text").Value;
                                        if (element == "button")
                                            ui.Text = doc.Root.Element("state").Attribute("title").Value;
                                        ui.FontSize = doc.Root.Element("fontDescription").Attribute("pointSize").Value;
                                        /*if (docTS.Contains("cocoaTouchSystemColor"))
                                            ui.TextColor = doc.Root.Element("color").Attribute("cocoaTouchSystemColor").Value;*/
                                        if (string.IsNullOrEmpty(ui.TextColor) && element == "button")
                                        {
                                            ui.ColorA = nDoc.Element("color").Attribute("alpha").Value;
                                            ui.ColorB = nDoc.Element("color").Attribute("blue").Value;
                                            ui.ColorG = nDoc.Element("color").Attribute("green").Value;
                                            ui.ColorR = nDoc.Element("color").Attribute("red").Value;
                                            if (nDoc.ToString().Contains("white"))
                                                ui.ColorW = nDoc.Element("color").Attribute("white").Value;
                                        }
                                        if (docTS.Contains("imageView"))
                                            ui.OtherDetails = doc.Root.Element("imageView").Attribute("image").Value;
                                        if (docTS.Contains("contentHorizontalAlignment"))
                                            ui.TextHAlign = doc.Root.Attribute("contentHorizontalAlignment").Value;
                                        if (docTS.Contains("contentVerticalAlignment"))
                                            ui.TextVAlign = doc.Root.Attribute("contentVerticalAlignment").Value;
                                        OutputUIElement(ui);
                                        ui = null;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                CreateFooter();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception : {0}:{1}", ex.Message, ex.StackTrace);
                CreateFooter();
            }
        }

        private void CreateHeader()
        {
            using (var writer = File.CreateText(outfile))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                writer.WriteLine("<ContentPage xmlns=\"http://xamarin.com/schemas/2014/forms\"");
                writer.WriteLine("xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
                writer.WriteLine("x:Class=\"HelloXamarinFormsWorldXaml.AbsoluteLayoutExample\"");
                writer.WriteLine("Padding=\"20\">");
                writer.WriteLine();
                writer.WriteLine("<AbsoluteLayout>");
                writer.WriteLine();
            }
        }

        private void CreateFooter()
        {
            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLine("</AbsoluteLayout>");
                writer.WriteLine("</ContentPage>");
            }
        }

        private void OutputUIElement(UIObject ui)
        {
            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLine(string.Format("<{0}", ui.UIElement));
                if (!string.IsNullOrEmpty(ui.UIName))
                    writer.WriteLine(string.Format("x:Name=\"{0}\"", ui.UIName));
                if (!string.IsNullOrEmpty(ui.Text))
                    writer.WriteLine(string.Format("Text=\"{0}\"", ui.Text));
                if (!string.IsNullOrEmpty(ui.FontSize))
                    writer.WriteLine(string.Format("Font=\"{0}\"", ui.FontSize));

                writer.WriteLine(string.Format("AbsoluteLayout.LayoutBounds=\"{0},{1},{2},{3}\"", ui.UIXPos, ui.UIYPos, ui.UIWidth, ui.UIHeight));

                if (!string.IsNullOrEmpty(ui.BackgroundColor))
                    writer.WriteLine(string.Format("BackgroundColor=\"{0}\"", ui.BackgroundColor));
                if (!string.IsNullOrEmpty(ui.TextColor))
                    writer.WriteLine(string.Format("TextColor=\"{0}\"", ui.TextColor));
                else
                {
                    if (string.IsNullOrEmpty(ui.ColorA))
                    {
                        int r = (int)(255 * double.Parse(ui.ColorR));
                        int g = (int)(255 * double.Parse(ui.ColorG));
                        int b = (int)(255 * double.Parse(ui.ColorB));
                        writer.WriteLine(string.Format("TextColor=\"#{0}{1}{2}\"", r.ToString("X"), g.ToString("X"), b.ToString("X")));
                    }
                    else
                    {
                        int a = (int)(255 * double.Parse(ui.ColorA));
                        int r = (int)(255 * double.Parse(ui.ColorR));
                        int g = (int)(255 * double.Parse(ui.ColorG));
                        int b = (int)(255 * double.Parse(ui.ColorB));
                        writer.WriteLine(string.Format("TextColor=\"#{0}{1}{2}{3}\"", a.ToString("X"), r.ToString("X"), g.ToString("X"), b.ToString("X")));
                    }
                        
                }
                if (!string.IsNullOrEmpty(ui.OtherDetails))
                    writer.WriteLine(ui.OtherDetails);
                writer.WriteLine(" />");
                writer.WriteLine();
            }
        }
    }
}
