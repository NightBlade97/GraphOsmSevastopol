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

        private static readonly double major = 6378137.0;
        private static readonly double minor = 6356752.3142;
        private static readonly double ratio = minor / major;
        private static readonly double e = Math.Sqrt(1.0 - (ratio * ratio));
        private static readonly double com = 0.5 * e;
        private static readonly double degToRad = Math.PI / 180.0;

        private static int typeOfAlgo = 0;
        private static int typeOfMetr = 0;

        struct coord
        {
            public double lat;
            public double lon;
        }

        public struct point
        {
            public double x;
            public double y;
            public double weight;
            public long parent;
            public bool isVisited;
            public long id;


        }

        private static double minlon;
        private static double maxlon;
        private static double minlat;
        private static double maxlat;

        private static SortedDictionary<long, coord> Nodes = new SortedDictionary<long, coord>();
        private static SortedDictionary<long, List<long>> AddjestedList = new SortedDictionary<long, List<long>>();
        private static List<string> Valid = new List<string>() {"motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link", "secondary",
                                            "secondary_link", "tertiary", "tertiary_link", "unclassified", "road", "service", "living_street", "residential" };

        private static SortedDictionary<long, point> Points = new SortedDictionary<long, point>();
        private static SortedDictionary<long, point> visitedPoints = new SortedDictionary<long, point>();

        private static point[] Arr;
        private static List<long> idHospitals = new List<long>();

        static void ParseOsm(string path)
        {

            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(path);
            XmlElement xRoot = xDoc.DocumentElement;
            XmlNodeList nodes = xRoot.SelectNodes("node");
            maxlat = double.Parse(xRoot.SelectSingleNode("bounds").Attributes["maxlat"].Value, CultureInfo.InvariantCulture);
            minlon = double.Parse(xRoot.SelectSingleNode("bounds").Attributes["minlon"].Value, CultureInfo.InvariantCulture);
            maxlon = double.Parse(xRoot.SelectSingleNode("bounds").Attributes["maxlon"].Value, CultureInfo.InvariantCulture);
            minlat = double.Parse(xRoot.SelectSingleNode("bounds").Attributes["minlat"].Value, CultureInfo.InvariantCulture);
            foreach (XmlNode n in nodes)
            {
                long id = long.Parse(n.SelectSingleNode("@id").Value);
                double lat = double.Parse(n.SelectSingleNode("@lat").Value, CultureInfo.InvariantCulture);
                double lon = double.Parse(n.SelectSingleNode("@lon").Value, CultureInfo.InvariantCulture);
                coord Node_coord;
                Node_coord.lat = lat;
                Node_coord.lon = lon;
                Nodes.Add(id, Node_coord);
            }
            Valid.Sort();
            XmlNodeList ways = xRoot.SelectNodes("//way[.//tag[@k = 'highway']]");
            foreach (XmlNode n in ways)
            {
                string validway = n.SelectSingleNode("tag[@k = 'highway']").Attributes["v"].Value;
                if (Valid.BinarySearch(validway) >= 0)
                {
                    XmlNodeList nd = n.SelectNodes("nd");
                    List<long> nodes_list_id = new List<long>();
                    foreach (XmlNode m in nd)
                    {
                        long id = long.Parse(m.SelectSingleNode("@ref").Value);
                        nodes_list_id.Add(id);
                    }
                    for (int i = 0; i < nodes_list_id.Count(); ++i)
                    {
                        if (i < nodes_list_id.Count() - 1)
                        {
                            if (AddjestedList.ContainsKey(nodes_list_id[i]))
                            {
                                AddjestedList[nodes_list_id[i]].Add(nodes_list_id[i + 1]);
                            }
                            else
                            {
                                AddjestedList.Add(nodes_list_id[i], new List<long>());
                                AddjestedList[nodes_list_id[i]].Add(nodes_list_id[i + 1]);
                            }
                        }
                        if (i >= 1)
                        {
                            if (AddjestedList.ContainsKey(nodes_list_id[i]))
                            {
                                AddjestedList[nodes_list_id[i]].Add(nodes_list_id[i - 1]);
                            }
                            else
                            {
                                AddjestedList.Add(nodes_list_id[i], new List<long>());
                                AddjestedList[nodes_list_id[i]].Add(nodes_list_id[i - 1]);
                            }
                        }
                    }
                }
            }
            XmlNodeList hospitals = xRoot.SelectNodes("//node[.//tag[@k = 'amenity']]");
            int count = 0;
            foreach (XmlNode n in hospitals)
            {
                string type = n.SelectSingleNode("tag[@k = 'amenity']").Attributes["v"].Value;
                if (type == "hospital")
                {
                    double lat = double.Parse(n.SelectSingleNode("@lat").Value, CultureInfo.InvariantCulture);
                    double lon = double.Parse(n.SelectSingleNode("@lon").Value, CultureInfo.InvariantCulture);
                    long id = findNearestPointToCoordinates(lat, lon);
                    idHospitals.Add(id);
                    count++;
                }
                if (count > 9)
                    break;
            }
        }

        

        private static double DegToRad(double deg)
        {
            return deg * degToRad;
        }

        public static double lonToX(double lon)
        {
            return major * DegToRad(lon) * 0.05;
        }

        public static double latToY(double lat)
        {
            lat = Math.Min(89.5, Math.Max(lat, -89.5));
            double phi = DegToRad(lat);
            double sinphi = Math.Sin(phi);
            double con = e * sinphi;
            con = Math.Pow(((1.0 - con) / (1.0 + con)), com);
            double ts = Math.Tan(0.5 * ((Math.PI * 0.5) - phi)) / con;
            return 0 - major * Math.Log(ts) * 0.05;
        }


        private static List<long>[] DijkstraWays = new List<long>[10];
        private static List<double> DijkstraWeight = new List<double>();

        static void DijkstraAlgorithm(long begin_node)
        {
            //Создание списка вершин
            ICollection<long> keys = AddjestedList.Keys;
            int N = keys.Count();
            Arr = new point[N];
            //long[] Numb = new long[keys.Count()];
            for (int i = 0; i < 10; i++)
            {
                DijkstraWays[i] = new List<long>();
            }
            int k = 0;
            Arr[k].id = begin_node;
            Arr[k].x = lonToX(Nodes[begin_node].lon);
            Arr[k].y = latToY(Nodes[begin_node].lat);
            Arr[k].weight = 0;
            Arr[k].parent = 0;
            Arr[k].isVisited = false;
            k++;
            foreach (long i in keys)
            {
                if (i != begin_node)
                {
                    Arr[k].id = i;
                    Arr[k].x = lonToX(Nodes[i].lon);
                    Arr[k].y = latToY(Nodes[i].lat);
                    Arr[k].weight = Double.PositiveInfinity;
                    Arr[k].parent = 0;
                    Arr[k].isVisited = false;
                    k++;
                }
            }

            for (int j = 0; j < keys.Count(); ++j)
            {
                double minWeight = Double.PositiveInfinity;
                long currPoint = 0;
                int iCurrPoint = 0;
                for (int i = 0; i < N; ++i)
                {
                    if (Arr[i].weight != Double.PositiveInfinity && Arr[i].weight < minWeight && !Arr[i].isVisited)
                    {
                        minWeight = Arr[i].weight;
                        currPoint = Arr[i].id;
                        iCurrPoint = i;
                    }
                }
                if (currPoint != 0)
                {
                    for (int i = 0; i < AddjestedList[currPoint].Count(); ++i)
                    {
                        long nextPoint = AddjestedList[currPoint][i];
                        int iNextPoint = 0;
                        for (int p = 1; p < N; ++p)
                        {
                            if (Arr[p].id == nextPoint)
                            {
                                iNextPoint = p;
                                break;
                            }
                        }
                        if (!Arr[iNextPoint].isVisited)
                        {
                            double weightCurrEdge = Math.Sqrt(Math.Pow(Arr[iCurrPoint].x - Arr[iNextPoint].x, 2.0) + Math.Pow(Arr[iCurrPoint].y - Arr[iNextPoint].y, 2.0));
                            if (Arr[iNextPoint].weight > Arr[iCurrPoint].weight + weightCurrEdge)
                            {
                                Arr[iNextPoint].weight = Arr[iCurrPoint].weight + weightCurrEdge;
                                Arr[iNextPoint].parent = iCurrPoint;//записываем номер ячейки массива!!!!!!!
                            }
                        }
                    }
                    Arr[iCurrPoint].isVisited = true;
                }
                else
                {
                    break;
                }
                // Console.WriteLine(j);
            }
            for (int l = 0; l < 10; l++)
            {
                DijkstraWeight.Add(FindDijkstraWay(l, idHospitals[l], N));

            }

        }


        static double FindDijkstraWay(int numbWay, long EndPoint, long N)
        {
            long iWay = -1;
            double weight = -1;
            for (int i = 0; i < N; ++i)
                if (Arr[i].id == EndPoint)
                {
                    iWay = i;
                    weight = Arr[i].weight;
                    break;
                }
            if (iWay == -1)
            {
                Console.WriteLine("Нет пути до точки с номером {0} !", numbWay);
                return 0;
            }
            while (iWay != 0)
            {
                DijkstraWays[numbWay].Add(Arr[iWay].id);
                iWay = Arr[iWay].parent;
            }
            DijkstraWays[numbWay].Add(Arr[iWay].id);
            foreach (long i in DijkstraWays[numbWay])
            {
                Console.WriteLine(i);
            }
            Console.WriteLine("Вес {0}", weight);
            return weight;
        }

        private static List<long>[] LevitWays = new List<long>[10];
        private static List<double> LevitWeigth = new List<double>();
        private static point[] ArrLevit;
        



        static void LevitAlgorithm(long startPoint)
        {
            //инициализация листов и очередей 
            List<long> Counted = new List<long>();
            List<long> NotCounted = new List<long>();
            Queue<long> MainQueue = new Queue<long>();
            Queue<long> UrgentQueue = new Queue<long>();

            ICollection<long> keys = AddjestedList.Keys;
            long N = keys.Count();
            ArrLevit = new point[N];

            long k = 0;
            ArrLevit[k].id = startPoint;
            ArrLevit[k].x = lonToX(Nodes[startPoint].lon);
            ArrLevit[k].y = latToY(Nodes[startPoint].lat);
            ArrLevit[k].weight = 0;
            ArrLevit[k].parent = 0;
            ArrLevit[k].isVisited = false;
            MainQueue.Enqueue(startPoint);
            k++;
            foreach (long i in keys)
            {
                if (i != startPoint)
                {
                    ArrLevit[k].id = i;
                    ArrLevit[k].x = lonToX(Nodes[i].lon);
                    ArrLevit[k].y = latToY(Nodes[i].lat);
                    ArrLevit[k].weight = Double.PositiveInfinity;
                    ArrLevit[k].parent = 0;
                    ArrLevit[k].isVisited = false;
                    NotCounted.Add(i);
                    k++;
                }
            }

            //алгоритм Левита 
            while (MainQueue.Count() != 0 || UrgentQueue.Count() != 0)
            {
                long idCurrPoint;
                if (UrgentQueue.Count() != 0)
                {
                    idCurrPoint = UrgentQueue.Dequeue();
                }
                else
                {
                    idCurrPoint = MainQueue.Dequeue();
                }
                int iCurrPoint = -1; ;
                for (int i = 0; i < N; ++i)
                {
                    if (ArrLevit[i].id == idCurrPoint)
                    {
                        iCurrPoint = i;
                        break;
                    }
                }
                if (iCurrPoint == -1) {
                    Console.WriteLine("Ошибка в iCurrPoint!");
                    break;
                }
                for (int i = 0; i < AddjestedList[idCurrPoint].Count(); ++i)
                {
                    long idNextPoint = AddjestedList[idCurrPoint][i];
                    int iNextPoint = -1; ;
                    for (int p = 0; p < N; ++p)
                    {
                        if (ArrLevit[p].id == idNextPoint)
                        {
                            iNextPoint = p;
                            break;
                        }
                    }
                    if (iCurrPoint == -1) {
                        Console.WriteLine("Ошибка в iCurrPoint!");
                        break;
                    }

                    double weightCurrEdge = Math.Sqrt(Math.Pow(ArrLevit[iCurrPoint].x - ArrLevit[iNextPoint].x, 2.0) + Math.Pow(ArrLevit[iCurrPoint].y - ArrLevit[iNextPoint].y, 2.0));

                    if (NotCounted.Contains(idNextPoint))
                    {
                        MainQueue.Enqueue(idNextPoint);
                        NotCounted.Remove(idNextPoint);
                        if (ArrLevit[iNextPoint].weight > ArrLevit[iCurrPoint].weight + weightCurrEdge)
                        {
                            ArrLevit[iNextPoint].weight = ArrLevit[iCurrPoint].weight + weightCurrEdge;
                            ArrLevit[iNextPoint].parent = iCurrPoint;
                        }
                    }
                    else
                    {
                        if (UrgentQueue.Contains(idNextPoint) || MainQueue.Contains(idNextPoint))
                        {
                            if (ArrLevit[iNextPoint].weight > ArrLevit[iCurrPoint].weight + weightCurrEdge)
                            {
                                ArrLevit[iNextPoint].weight = ArrLevit[iCurrPoint].weight + weightCurrEdge;
                                ArrLevit[iNextPoint].parent = iCurrPoint;
                            }
                        }
                        else
                        {
                             if (Counted.Contains(idNextPoint) && ArrLevit[iNextPoint].weight > ArrLevit[iCurrPoint].weight + weightCurrEdge)
                                {
                                    UrgentQueue.Enqueue(idNextPoint);
                                    Counted.Remove(idNextPoint);
                                    ArrLevit[iNextPoint].weight = ArrLevit[iCurrPoint].weight + weightCurrEdge;
                                    ArrLevit[iNextPoint].parent = iCurrPoint;
                                }
                            
                        }
                    }
                }
                Counted.Add(idCurrPoint);
            }

            for (int i = 0; i < 10; i++)
            {
                LevitWays[i] = new List<long>();
            }
            //long idWay1 = 265665403;
            for (int l = 0; l < 10; l++)
            {
                LevitWeigth.Add(FindLevitWay(l, idHospitals[l], N));
            }
            //FindLevitWay(0, idWay1, N);
        }


        static double FindLevitWay(int numbWay, long EndPoint, long N)
        {
            long iWay = -1;
            double weight = -1;
            for (long i = 0; i < N; ++i)
                if (ArrLevit[i].id == EndPoint)
                {
                    iWay = i;
                    weight = ArrLevit[i].weight;
                    break;
                }
            if (iWay == -1)
            {
                Console.WriteLine("Нет пути до то точки с номером {0} !", numbWay);
                return 0;
            }
            while (iWay != 0)
            {
                LevitWays[numbWay].Add(ArrLevit[iWay].id);
                iWay = ArrLevit[iWay].parent;
            }
            LevitWays[numbWay].Add(ArrLevit[iWay].id);
           /* foreach (long i in LevitWays[numbWay])
            {
                Console.WriteLine(i);
            }
            Console.WriteLine("Вес {0}", weight);*/
            return weight;
        }




        struct pointAStar
        {
            public long id;
            public double X;
            public double Y;
            public double weight;
            public double h;
            public double f; //f=h+weight
            public int prevPoint;
            public bool isChecked;
        }
        private static List<long>[] AStarWays = new List<long>[10];
        private static List<double> AStarWeigth = new List<double>();
        private static pointAStar[] AStarArr;
        
        static double FuncH(int iThisPoint, int iEndPoint)
        {
            if (typeOfMetr == 1)
            {
                return Math.Abs(AStarArr[iThisPoint].X - AStarArr[iEndPoint].X) + Math.Abs(AStarArr[iThisPoint].Y - AStarArr[iEndPoint].Y);
            }
            if (typeOfMetr == 2)
            {
                 return Math.Max(Math.Abs(AStarArr[iThisPoint].X - AStarArr[iEndPoint].X), Math.Abs(AStarArr[iThisPoint].Y - AStarArr[iEndPoint].Y));
            }
            if (typeOfMetr == 3)
            {
                return Math.Sqrt(Math.Pow(Math.Abs(AStarArr[iThisPoint].X - AStarArr[iEndPoint].X), 2.0) + Math.Pow(Math.Abs(AStarArr[iThisPoint].Y - AStarArr[iEndPoint].Y), 2.0));
            }
            return Math.Abs(AStarArr[iThisPoint].X - AStarArr[iEndPoint].X) + Math.Abs(AStarArr[iThisPoint].Y - AStarArr[iEndPoint].Y);
        }
        static void AStarAlgorithm(long startPoint, long EndPoint, int numbOfWay)
        {
            //инициализация
            ICollection<long> keys = AddjestedList.Keys;
            int N = keys.Count();
            AStarArr = new pointAStar[N];
            int k = 0;
            AStarArr[k].id = startPoint;
            AStarArr[k].X = lonToX(Nodes[startPoint].lon);
            AStarArr[k].Y = latToY(Nodes[startPoint].lat);
            AStarArr[k].weight = 0;
            AStarArr[k].h = 0;
            AStarArr[k].f = 0;
            AStarArr[k].prevPoint = 0;
            AStarArr[k].isChecked = false;
            k++;
            int iEndPoint = -1;
            foreach (long i in keys)
            {
                if (i != startPoint)
                {
                    if (i == EndPoint) iEndPoint = k;
                    AStarArr[k].id = i;
                    AStarArr[k].X = lonToX(Nodes[i].lon);
                    AStarArr[k].Y = latToY(Nodes[i].lat);
                    AStarArr[k].weight = Double.PositiveInfinity;
                    AStarArr[k].h = Double.PositiveInfinity;
                    AStarArr[k].f = Double.PositiveInfinity;
                    AStarArr[k].prevPoint = 0;
                    AStarArr[k].isChecked = false;
                    k++;
                }
            }
            if (iEndPoint == -1) {
                Console.WriteLine("Ошибка в EndPoint id");
                return;
            }
            //алгоритм A*
            for (int j = 0; j < keys.Count(); ++j)
            {
                double minF = Double.PositiveInfinity;
                long currPoint = 0;
                int iCurrPoint = 0;
                for (int i = 0; i < N; ++i)
                {
                    if (AStarArr[i].f != Double.PositiveInfinity && AStarArr[i].f < minF && !AStarArr[i].isChecked)
                    {
                        minF = AStarArr[i].f;
                        currPoint = AStarArr[i].id;
                        iCurrPoint = i;
                    }
                }
                if (currPoint == EndPoint)//Дошли до нужной точки
                { //Ищем пути от конечных точек
                    AStarWays[numbOfWay] = new List<long>();
                    AStarWeigth.Add(FindAStarWay(numbOfWay, EndPoint, N));
                    return;
                }

                if (currPoint != 0)
                {
                    for (int i = 0; i < AddjestedList[currPoint].Count(); ++i)
                    {
                        long nextPoint = AddjestedList[currPoint][i];
                        int iNextPoint = 0;
                        for (int p = 1; p < N; ++p)
                        {
                            if (AStarArr[p].id == nextPoint)
                            {
                                iNextPoint = p;
                                break;
                            }
                        }
                        if (!AStarArr[iNextPoint].isChecked)
                        {
                            double weightCurrEdge = Math.Sqrt(Math.Pow(AStarArr[iCurrPoint].X - AStarArr[iNextPoint].X, 2.0) + Math.Pow(AStarArr[iCurrPoint].Y - AStarArr[iNextPoint].Y, 2.0));
                            if (AStarArr[iNextPoint].weight > AStarArr[iCurrPoint].weight + weightCurrEdge)
                            {
                                AStarArr[iNextPoint].weight = AStarArr[iCurrPoint].weight + weightCurrEdge;
                                AStarArr[iNextPoint].h = FuncH(iNextPoint, iEndPoint);
                                AStarArr[iNextPoint].f = AStarArr[iNextPoint].weight + AStarArr[iNextPoint].h;
                                AStarArr[iNextPoint].prevPoint = iCurrPoint;//записываем номер ячейки массива!!!!!!!
                            }
                        }
                    }
                    AStarArr[iCurrPoint].isChecked = true;
                }
                else
                {
                    break;
                }
            }
        }

        static double FindAStarWay(int numbWay, long EndPoint, int N)
        {
            int iWay = -1;
            double weight = -1;
            for (int i = 0; i < N; ++i)
                if (AStarArr[i].id == EndPoint)
                {
                    iWay = i;
                    weight = AStarArr[i].weight;
                    break;
                }
            if (iWay == -1)
            {
                Console.WriteLine("Нет пути к вершине с номером {0}!", numbWay);
                return 0;
            }
            while (iWay != 0)
            {
                AStarWays[numbWay].Add(AStarArr[iWay].id);
                iWay = AStarArr[iWay].prevPoint;
            }
            AStarWays[numbWay].Add(AStarArr[iWay].id);
            /*foreach (long i in AStarWays[numbWay])
            {
                Console.WriteLine(i);
            }
            Console.WriteLine("Вес {0}", weight);*/
            return weight;
        }


        

        static void WriteCsv()
        {
            string pathСsv;
            Console.WriteLine("Введите желаемый путь для csv файла(без расширения) маршрутов от выбранной точки до 10 больниц в городе:");
            pathСsv = Console.ReadLine();
            System.IO.StreamWriter outputFile = new System.IO.StreamWriter(pathСsv + ".csv");
            outputFile.WriteLine("Node;Way");
            
            for (int i=0;i<10;++i)
            {
                string newLine = "";
                newLine += idHospitals[i];
                newLine += ";";
                newLine += "{";
                if (typeOfAlgo == 3)
                {
                    for (int j = AStarWays[i].Count() - 1; j >= 0; j--)
                    {
                        newLine += AStarWays[i][j];
                        newLine += ",";
                    }
                }
                if (typeOfAlgo == 2)
                {
                    for (int j = LevitWays[i].Count() - 1; j >= 0; j--)
                    {
                        newLine += LevitWays[i][j];
                        newLine += ",";
                    }
                }
                if (typeOfAlgo == 1)
                {
                    for (int j = DijkstraWays[i].Count() - 1; j >= 0; j--)
                    {
                        newLine += DijkstraWays[i][j];
                        newLine += ",";
                    }
                }
                newLine += "}";
                outputFile.WriteLine(newLine);
            }
            outputFile.Close();
        }

        static void WriteSvg(long startPointId)
        {
            string pathSvg;
            Console.WriteLine("Введите желаемый путь для svg файла(без расширения) маршрутов от выбранной точки до 10 больниц в городе:");
            pathSvg = Console.ReadLine();
            System.IO.StreamWriter outputFile = new System.IO.StreamWriter(pathSvg + ".svg");
            outputFile.WriteLine("<svg version = \"1.1\" baseProfile = \"full\" xmlns = \"http://www.w3.org/2000/svg\" >");
            double min_weight=1000000;
            //Выводим стартовую вершину
            string newLine = "<circle ";
            newLine += "cx=\"" + System.Convert.ToString(lonToX(Nodes[startPointId].lon) - lonToX(minlon)).Replace(",", ".")+"\" cy=\""+ System.Convert.ToString(-latToY(Nodes[startPointId].lat) + latToY(maxlat)).Replace(",", ".")+"\" r=\"10\" fill=\"blue\" />";
            outputFile.WriteLine(newLine);
            //Ищем минимальный вес
            int min_i = 0;
            for(int i = 1; i < 10; ++i)
            {
                if (typeOfAlgo == 3)
                {
                    if (AStarWeigth[i] < AStarWeigth[min_i])
                    {
                        min_i = i;
                    }
                }
                if (typeOfAlgo == 2)
                {
                    if (LevitWeigth[i] < LevitWeigth[min_i])
                    {
                        min_i = i;
                    }
                }
                if (typeOfAlgo == 1)
                {
                    if (DijkstraWeight[i] < DijkstraWeight[min_i])
                    {
                        min_i = i;
                    }
                }
                

            }


            //Выводим конечные вершины
            for (int i = 0; i < 10; ++i)
            {
                if (i == min_i)
                {
                    newLine = "<circle ";
                    newLine += "cx=\"" + System.Convert.ToString(lonToX(Nodes[idHospitals[i]].lon) - lonToX(minlon)).Replace(",", ".") + "\" cy=\"" + System.Convert.ToString(-latToY(Nodes[idHospitals[i]].lat) + latToY(maxlat)).Replace(",", ".") + "\" r=\"10\" fill=\"green\" />";
                    outputFile.WriteLine(newLine);
                }
                else
                {
                    newLine = "<circle ";
                    newLine += "cx=\"" + System.Convert.ToString(lonToX(Nodes[idHospitals[i]].lon) - lonToX(minlon)).Replace(",", ".") + "\" cy=\"" + System.Convert.ToString(-latToY(Nodes[idHospitals[i]].lat) + latToY(maxlat)).Replace(",", ".") + "\" r=\"10\" fill=\"red\" />";
                    outputFile.WriteLine(newLine);
                }
            }


            ICollection<long> keys = AddjestedList.Keys;
            foreach (long i in keys)
            {
                for (int j = 0; j < AddjestedList[i].Count(); ++j)
                {
                    string LineToSvg = "<line ";
                    LineToSvg += "x1=\"" + System.Convert.ToString(lonToX(Nodes[i].lon) - lonToX(minlon)).Replace(",", ".") + "\" x2=\"" + System.Convert.ToString(lonToX(Nodes[AddjestedList[i][j]].lon) - lonToX(minlon)).Replace(",", ".") + "\" y1=\"" + System.Convert.ToString(-latToY(Nodes[i].lat) + latToY(maxlat)).Replace(",", ".") + "\" y2=\"" + System.Convert.ToString(-latToY(Nodes[AddjestedList[i][j]].lat) + latToY(maxlat)).Replace(",", ".") + "\" ";
                    LineToSvg += "stroke = \"black\" stroke-width= \"1\" />";
                    int k = 0;
                    outputFile.WriteLine(LineToSvg);
                }

            }


            for (int i=0;i<10; ++i)
            {
                for (int j = 0; j < AStarWays[i].Count()-1; ++j)
                {
                     newLine = "<line ";
                    if (typeOfAlgo == 3)
                    {
                        newLine += "x1=\"" + System.Convert.ToString(lonToX(Nodes[AStarWays[i][j]].lon) - lonToX(minlon)).Replace(",", ".") + "\" x2=\"" + System.Convert.ToString(lonToX(Nodes[AStarWays[i][j + 1]].lon) - lonToX(minlon)).Replace(",", ".") + "\" y1=\"" + System.Convert.ToString(-latToY(Nodes[AStarWays[i][j]].lat) + latToY(maxlat)).Replace(",", ".") + "\" y2=\"" + System.Convert.ToString(-latToY(Nodes[AStarWays[i][j + 1]].lat) + latToY(maxlat)).Replace(",", ".") + "\" ";
                    }
                    if (typeOfAlgo == 2)
                    {
                        newLine += "x1=\"" + System.Convert.ToString(lonToX(Nodes[LevitWays[i][j]].lon) - lonToX(minlon)).Replace(",", ".") + "\" x2=\"" + System.Convert.ToString(lonToX(Nodes[LevitWays[i][j + 1]].lon) - lonToX(minlon)).Replace(",", ".") + "\" y1=\"" + System.Convert.ToString(-latToY(Nodes[LevitWays[i][j]].lat) + latToY(maxlat)).Replace(",", ".") + "\" y2=\"" + System.Convert.ToString(-latToY(Nodes[LevitWays[i][j + 1]].lat) + latToY(maxlat)).Replace(",", ".") + "\" ";
                    }
                    if (typeOfAlgo == 1)
                    {
                        newLine += "x1=\"" + System.Convert.ToString(lonToX(Nodes[DijkstraWays[i][j]].lon) - lonToX(minlon)).Replace(",", ".") + "\" x2=\"" + System.Convert.ToString(lonToX(Nodes[DijkstraWays[i][j + 1]].lon) - lonToX(minlon)).Replace(",", ".") + "\" y1=\"" + System.Convert.ToString(-latToY(Nodes[DijkstraWays[i][j]].lat) + latToY(maxlat)).Replace(",", ".") + "\" y2=\"" + System.Convert.ToString(-latToY(Nodes[DijkstraWays[i][j + 1]].lat) + latToY(maxlat)).Replace(",", ".") + "\" ";
                    }
                    
                    newLine += "stroke = \"blue\" stroke-width= \"5\" />";
                    outputFile.WriteLine(newLine);
                }

            }
            outputFile.WriteLine("</svg>");
            outputFile.Close();
        }



        static long findNearestPointToCoordinates(double lat,double lon)
        {
            long id=0;
            double min_razl = 100000;
            ICollection<long> keys = AddjestedList.Keys;
            foreach(long i in keys)
            {
                if ((Math.Abs(Nodes[i].lat - lat) + Math.Abs(Nodes[i].lon - lon)) < min_razl)
                {
                    id = i;
                    min_razl = Math.Abs(Nodes[i].lat - lat) + Math.Abs(Nodes[i].lon - lon);
                }
            }

            return id;
        }



        static void Main(string[] args)
        {
            string path;
            Console.WriteLine("Введите полный путь необходимого для обработки osm файла:");
            path = Console.ReadLine();
            ParseOsm(path);
            Console.WriteLine("Введите значение LAT для точки, от которой мы будем искать пути(значение от {0} до {1})",minlat ,maxlat);
            double lat=Double.Parse(Console.ReadLine());
            Console.WriteLine("Введите значение LON для точки, от которой мы будем искать пути(значение от {0} до {1})", minlon, maxlon);
            double lon = Double.Parse(Console.ReadLine());
            long startPointId = findNearestPointToCoordinates(lat,lon);

            Console.WriteLine("Введите 1 для поиска кратчайших путей между вершинами с помощью алгоритма Дейкстры");
            Console.WriteLine("Введите 2 для поиска кратчайших путей между вершинами с помощью алгоритма Левита");
            Console.WriteLine("Введите 3 для поиска кратчайших путей между вершинами с помощью алгоритма A-star");
            typeOfAlgo = int.Parse(Console.ReadLine());
            if (typeOfAlgo == 1)
            {
                Console.WriteLine("Выполняется поиск кратчайших путей между вершинами с помощью алгоритма Дейкстры");
                DijkstraAlgorithm(startPointId);
            }
            if (typeOfAlgo == 2)
            {
                Console.WriteLine("Выполняется поиск кратчайших путей между вершинами с помощью алгоритма Левита");
                LevitAlgorithm(startPointId);
            }
            if (typeOfAlgo == 3)
            {

                Console.WriteLine("Введите 1 для использования Мантхэттенского расстояния");
                Console.WriteLine("Введите 2 для использования расстояния Чебышева");
                Console.WriteLine("Введите 3 для использования Евклидова расстояния");

                typeOfMetr = int.Parse(Console.ReadLine());

                Console.WriteLine("Выполняется поиск кратчайших путей между вершинами с помощью алгоритма A-star");

                for (int l = 0; l < 10; l++)
                {
                    AStarAlgorithm(startPointId, idHospitals[l], l);
                }
            }
            
            WriteCsv();
            WriteSvg(startPointId);
            //Console.ReadLine();
           
        }
    }
}
