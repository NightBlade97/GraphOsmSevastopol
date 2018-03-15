using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


namespace GraphOpenStreetMap
{
    class Program
    {

        private static readonly double R_MAJOR = 6378137.0;
        private static readonly double R_MINOR = 6356752.3142;
        private static readonly double RATIO = R_MINOR / R_MAJOR;
        private static readonly double ECCENT = Math.Sqrt(1.0 - (RATIO * RATIO));
        private static readonly double COM = 0.5 * ECCENT;

        private static readonly double DEG2RAD = Math.PI / 180.0;
        private static readonly double PI_2 = Math.PI / 2.0;


        struct point
        {
            public double lat;
            public double lon;
        }


        private static double minlon;
        private static double maxlat;

        private static SortedDictionary<long, point> Nodes = new SortedDictionary<long, point>();
        private static SortedDictionary<long, List<long>> AddjestedList = new SortedDictionary<long, List<long>>();
        private static List<string> ValidHighways = new List<string>() {"motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link", "secondary",
                 "secondary_link", "tertiary", "tertiary_link", "unclassified", "road", "service", "living_street",
                 "residential" };

        static void ParseOsm(string pathOsmFile)
        {
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(pathOsmFile);
            XmlElement xRoot = xDoc.DocumentElement;

            maxlat= double.Parse(xRoot.SelectSingleNode("bounds").Attributes["maxlat"].Value, CultureInfo.InvariantCulture);
            minlon = double.Parse(xRoot.SelectSingleNode("bounds").Attributes["minlon"].Value, CultureInfo.InvariantCulture);

            XmlNodeList nodes = xRoot.SelectNodes("node");
            foreach (XmlNode n in nodes)
            {
                long id = long.Parse(n.SelectSingleNode("@id").Value);
                double lat = double.Parse(n.SelectSingleNode("@lat").Value, CultureInfo.InvariantCulture);
                double lon = double.Parse(n.SelectSingleNode("@lon").Value, CultureInfo.InvariantCulture);
                point Nodepoint;
                Nodepoint.lat = lat;
                Nodepoint.lon = lon;
                Nodes.Add(id,Nodepoint);
            }
            ValidHighways.Sort();
            XmlNodeList ways = xRoot.SelectNodes("//way[.//tag[@k = 'highway']]");
            foreach (XmlNode n in ways)
            {
                string highway = n.SelectSingleNode("tag[@k = 'highway']").Attributes["v"].Value; 
                if (ValidHighways.BinarySearch(highway) >= 0)
                {
                    XmlNodeList nd = n.SelectNodes("nd");
                    List<long> id_nodes = new List<long>();
                    foreach (XmlNode m in nd)
                    {
                        long id = long.Parse(m.SelectSingleNode("@ref").Value);
                        id_nodes.Add(id);
                    }
                    for (int i = 0; i < id_nodes.Count(); ++i)
                    {
                        if (i < id_nodes.Count()-1)
                        {
                           if (AddjestedList.ContainsKey(id_nodes[i]))
                            {
                                AddjestedList[id_nodes[i]].Add(id_nodes[i+1]);
                            }
                            else
                            {
                                AddjestedList.Add(id_nodes[i], new List<long>() );
                                AddjestedList[id_nodes[i]].Add(id_nodes[i + 1]);
                            }
                        }
                        if (i >= 1)
                        {
                            if (AddjestedList.ContainsKey(id_nodes[i]))
                            {
                                AddjestedList[id_nodes[i]].Add(id_nodes[i - 1]);
                            }
                            else
                            {
                                AddjestedList.Add(id_nodes[i], new List<long>());
                                AddjestedList[id_nodes[i]].Add(id_nodes[i - 1]);
                            }
                        }
                    }
                }
            }
        }

        static void WriteCsv()
        {
            string pathСsvFile;
            Console.WriteLine("Введите желаемый путь для csv файла списка смежности полученного графа(без расширения):");
            pathСsvFile = Console.ReadLine();
            System.IO.StreamWriter textFile = new System.IO.StreamWriter(pathСsvFile+".csv");
            textFile.WriteLine("Nodes;Addjested Nodes");
            ICollection<long> keys = AddjestedList.Keys;
            foreach (long i in keys)
            {
                string LineToCsv="";
                LineToCsv +=i;
                LineToCsv += ";";
                LineToCsv +="{";
                for (int j=0;j< AddjestedList[i].Count(); ++j)
                {
                    LineToCsv +=AddjestedList[i][j];
                    LineToCsv += ",";
                }
                LineToCsv += "}";
                textFile.WriteLine(LineToCsv);
            }
            textFile.Close();
        }

        private static double DegToRad(double deg)
        {
            return deg * DEG2RAD;
        }

        public static double lonToX(double lon)
        {
            return R_MAJOR * DegToRad(lon)*0.1;
        }

        public static double latToY(double lat)
        {
            lat = Math.Min(89.5, Math.Max(lat, -89.5));
            double phi = DegToRad(lat);
            double sinphi = Math.Sin(phi);
            double con = ECCENT * sinphi;
            con = Math.Pow(((1.0 - con) / (1.0 + con)), COM);
            double ts = Math.Tan(0.5 * ((Math.PI * 0.5) - phi)) / con;
            return 0 - R_MAJOR * Math.Log(ts)*0.1;
        }


        static void WriteSvg()
        {
            string pathСsvFile;
            Console.WriteLine("Введите желаемый путь для svg файла полученного графа(без расширения):");
            pathСsvFile = Console.ReadLine();
            System.IO.StreamWriter textFile = new System.IO.StreamWriter(pathСsvFile + ".svg");
            textFile.WriteLine("<svg version = \"1.1\" baseProfile = \"full\" xmlns = \"http://www.w3.org/2000/svg\" >");
            ICollection<long> keys = AddjestedList.Keys;
            foreach (long i in keys)
            {
                for (int j = 0; j < AddjestedList[i].Count(); ++j)
                {
                    string LineToSvg = "<line ";
                    LineToSvg +="x1=\""+ System.Convert.ToString(lonToX(Nodes[i].lon)-lonToX(minlon)).Replace(",",".")+"\" x2=\""+ System.Convert.ToString(lonToX(Nodes[AddjestedList[i][j]].lon)-lonToX(minlon)).Replace(",", ".") + "\" y1=\"" + System.Convert.ToString(-latToY(Nodes[i].lat)+latToY(maxlat)).Replace(",", ".") + "\" y2=\""+ System.Convert.ToString(-latToY(Nodes[AddjestedList[i][j]].lat)+latToY(maxlat)).Replace(",", ".") + "\" ";
                    LineToSvg += "stroke = \"blue\" stroke-width= \"1\" />";
                    int k = 0;
                    textFile.WriteLine(LineToSvg);
                }
                
            }
            textFile.WriteLine("</svg>");
            textFile.Close();
        }
        

        static void Main(string[] args)
        {
           
            string pathOsmFile;
            Console.WriteLine("Введите полный путь необходимого для обработки osm файла:");
            pathOsmFile=Console.ReadLine();
            ParseOsm(pathOsmFile);
            WriteCsv();
            WriteSvg();



        }


    }
}
