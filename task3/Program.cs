using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


namespace TheProblemOfTravellingSalesman
{
    class Program
    {

        private static readonly double major = 6378137.0;
        private static readonly double minor = 6356752.3142;
        private static readonly double ratio = minor / major;
        private static readonly double e = Math.Sqrt(1.0 - (ratio * ratio));
        private static readonly double com = 0.5 * e;
        private static readonly double degToRad = Math.PI / 180.0;

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
        private static SortedDictionary<long, List<long>> AddjestedList_for_output = new SortedDictionary<long, List<long>>();
        private static List<string> Valid = new List<string>() {"motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link", "secondary",
                                            "secondary_link", "tertiary", "tertiary_link", "unclassified", "road", "service", "living_street", "residential" };

        private static SortedDictionary<long, point> Points = new SortedDictionary<long, point>();
        private static SortedDictionary<long, point> visitedPoints = new SortedDictionary<long, point>();

        private static point[] Arr;
        private static List<long> idHospitals = new List<long>();
        private static SortedDictionary<long, bool> HospitalInformation = new SortedDictionary<long, bool>();

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
                    HospitalInformation.Add(id,false);
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
            return major * DegToRad(lon) * 0.1;
        }

        public static double latToY(double lat)
        {
            lat = Math.Min(89.5, Math.Max(lat, -89.5));
            double phi = DegToRad(lat);
            double sinphi = Math.Sin(phi);
            double con = e * sinphi;
            con = Math.Pow(((1.0 - con) / (1.0 + con)), com);
            double ts = Math.Tan(0.5 * ((Math.PI * 0.5) - phi)) / con;
            return 0 - major * Math.Log(ts) * 0.1;
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
        private static List<long>[] AStarWays = new List<long>[11];
        private static double[] AStarWeigth = new double[11];
        private static pointAStar[] AStarArr;

        static double FuncH(int iThisPoint, int iEndPoint)
        {
            return Math.Abs(AStarArr[iThisPoint].X - AStarArr[iEndPoint].X) + Math.Abs(AStarArr[iThisPoint].Y - AStarArr[iEndPoint].Y);
           // return Math.Max(Math.Abs(AStarArr[iThisPoint].X - AStarArr[iEndPoint].X), Math.Abs(AStarArr[iThisPoint].Y - AStarArr[iEndPoint].Y));
           // return Math.Sqrt(Math.Pow(Math.Abs(AStarArr[iThisPoint].X - AStarArr[iEndPoint].X),2.0)+ Math.Pow(Math.Abs(AStarArr[iThisPoint].Y - AStarArr[iEndPoint].Y), 2.0));

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
            if (iEndPoint == -1)
            {
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
                    AStarWeigth[numbOfWay]=FindAStarWay(numbOfWay, EndPoint, N);
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

            for (int i = 0; i < 10; ++i)
            {
                string newLine = "";
                newLine += idHospitals[i];
                newLine += ";";
                newLine += "{";
                for (int j = AStarWays[i].Count() - 1; j >= 0; j--)
                {
                    newLine += AStarWays[i][j];
                    newLine += ",";
                }
                newLine += "}";
                outputFile.WriteLine(newLine);
            }
            outputFile.Close();
        }

        static void WriteSvg(long startPointId)
        {
            string pathSvg;
            Console.WriteLine("Введите желаемый путь для svg файла(без расширения) для рисования маршрутов в задачи коммивояжера :");
            pathSvg = Console.ReadLine();
            System.IO.StreamWriter outputFile = new System.IO.StreamWriter(pathSvg + ".svg");
            outputFile.WriteLine("<svg version = \"1.1\" baseProfile = \"full\" xmlns = \"http://www.w3.org/2000/svg\" >");
            double min_weight = 1000000;
            //Выводим стартовую вершину
            string newLine = "<circle ";
            newLine += "cx=\"" + System.Convert.ToString(lonToX(Nodes[startPointId].lon) - lonToX(minlon)).Replace(",", ".") + "\" cy=\"" + System.Convert.ToString(-latToY(Nodes[startPointId].lat) + latToY(maxlat)).Replace(",", ".") + "\" r=\"10\" fill=\"blue\" />";
            outputFile.WriteLine(newLine);


            //Выводим конечные вершины
            for (int i = 0; i < 10; ++i)
            {
                if (i == orderOfHospitals[0])
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

            foreach (long i in AddjestedList.Keys)
            {
                for (int j = 0; j < AddjestedList[i].Count() - 1; ++j)
                {
                    newLine = "<line ";
                    newLine += "x1=\"" + System.Convert.ToString(lonToX(Nodes[AddjestedList_for_output[i][j]].lon) - lonToX(minlon)).Replace(",", ".") + "\" x2=\"" + System.Convert.ToString(lonToX(Nodes[AddjestedList_for_output[i][j + 1]].lon) - lonToX(minlon)).Replace(",", ".") + "\" y1=\"" + System.Convert.ToString(-latToY(Nodes[AddjestedList_for_output[i][j]].lat) + latToY(maxlat)).Replace(",", ".") + "\" y2=\"" + System.Convert.ToString(-latToY(Nodes[AddjestedList_for_output[i][j + 1]].lat) + latToY(maxlat)).Replace(",", ".") + "\" ";
                    newLine += "stroke = \"black\" stroke-width= \"1\" />";
                    outputFile.WriteLine(newLine);
                }

            }




            for (int i = 0; i < 11; ++i)
            {
                for (int j = 0; j < NeighborWays[i].Count() - 1; ++j)
                {
                    newLine = "<line ";
                    newLine += "x1=\"" + System.Convert.ToString(lonToX(Nodes[NeighborWays[i][j]].lon) - lonToX(minlon)).Replace(",", ".") + "\" x2=\"" + System.Convert.ToString(lonToX(Nodes[NeighborWays[i][j + 1]].lon) - lonToX(minlon)).Replace(",", ".") + "\" y1=\"" + System.Convert.ToString(-latToY(Nodes[NeighborWays[i][j]].lat) + latToY(maxlat)).Replace(",", ".") + "\" y2=\"" + System.Convert.ToString(-latToY(Nodes[NeighborWays[i][j + 1]].lat) + latToY(maxlat)).Replace(",", ".") + "\" ";

                    switch (i)
                    {
                        case 0:
                            newLine += "stroke = \"blue\" stroke-width= \"5\" />";
                            break;
                        case 1:
                            newLine += "stroke = \"yellow\" stroke-width= \"5\" />";
                            break;
                        case 2:
                            newLine += "stroke = \"brown\" stroke-width= \"5\" />";
                            break;
                        case 3:
                            newLine += "stroke = \"orange\" stroke-width= \"5\" />";
                            break;
                        case 4:
                            newLine += "stroke = \"pink\" stroke-width= \"5\" />";
                            break;
                        case 5:
                            newLine += "stroke = \"red\" stroke-width= \"5\" />";
                            break;
                        case 6:
                            newLine += "stroke = \"green\" stroke-width= \"5\" />";
                            break;
                        case 7:
                            newLine += "stroke = \"olive\" stroke-width= \"5\" />";
                            break;
                        case 8:
                            newLine += "stroke = \"purple\" stroke-width= \"5\" />";
                            break;
                        case 9:
                            newLine += "stroke = \"yellow\" stroke-width= \"5\" />";
                            break;
                        case 10:
                            newLine += "stroke = \"orange\" stroke-width= \"5\" />";
                            break;
  
                    }
                   
                    outputFile.WriteLine(newLine);
                }

            }
            outputFile.WriteLine("</svg>");
            outputFile.Close();
        }



        static long findNearestPointToCoordinates(double lat, double lon)
        {
            long id = 0;
            double min_razl = 100000;
            ICollection<long> keys = AddjestedList.Keys;
            foreach (long i in keys)
            {
                if ((Math.Abs(Nodes[i].lat - lat) + Math.Abs(Nodes[i].lon - lon)) < min_razl)
                {
                    id = i;
                    min_razl = Math.Abs(Nodes[i].lat - lat) + Math.Abs(Nodes[i].lon - lon);
                }
            }

            return id;
        }


        private static List<long>[] NeighborWays = new List<long>[11];

        private static int[] orderOfHospitals = new int[10]; 

        static void neighborMethod(long startPointId)
        {
            long lastPointId = startPointId;
            List<long> startPointAddList = new List<long> ();
            startPointAddList = AddjestedList[startPointId];
            for (int number = 0; number < 10; ++number)
            {
                double min_weigth_way = 10000000;
                int nextPointNumber = 0;
                for (int i = 0; i < 10; ++i)
                { 
                    if (!HospitalInformation[idHospitals[i]])
                    {   
                        AStarAlgorithm(lastPointId,idHospitals[i],i);
                        
                    }
                    else
                    {
                        AStarWeigth[i] = 10000000;
                    }
                    if (AStarWeigth[i] < min_weigth_way)
                    {
                        min_weigth_way = AStarWeigth[i];
                        nextPointNumber = i;
                    }
                }
                HospitalInformation[idHospitals[nextPointNumber]] = true;
                NeighborWays[number] = AStarWays[nextPointNumber];
                orderOfHospitals[number] = nextPointNumber;
                AddjestedList.Remove(lastPointId);
                lastPointId = idHospitals[nextPointNumber];
                
            }
            AddjestedList.Add(startPointId,startPointAddList);
            AStarAlgorithm(lastPointId,startPointId,10);
            NeighborWays[10] = AStarWays[10];
        }


        public class lobject
        {
            public int city;
            public double cost;
            public double[,] matrix;
            public int[] remainingcity;
            public int city_left_to_expand;
            public Stack<double> st;
            public lobject(int number)
            {
                matrix = new double[number, number];
                st = new Stack<double>();
            }
        }


        public static double[] min(double[] array, double min)
        {
            
            for (int j = 0; j < array.Length; j++)
            {
                array[j] = array[j] - min;
            }
            
            return array;
        }

        /*
        Minimum Function - Calculates the minimum value with which a matrix can be reduced
        Input - Array for which minimum value is to be calculated
        Return - Minimum value
        */
        public static double minimum(double[] array)
        {
            
            double min = 90000;
            
            for (int i = 0; i < array.Length; i++)
            {
                
                if (array[i] < min)
                {
                    min = array[i];
                }
            }
            
            if (min == 90000)
            {
                
                return 0;
            }
            
            else
            {
                return min;
            }
        }

        /*
    Reduce - Reduces the passed Matrix with minimum value possible
    Input - 2D Array to be reduced, Previous Step's Cost, Row to be processed, Column to be processed
    Return - Cost of Reduction
    */

        public static double reduce(double[,] array, double cost, int row, int column)
        {
            
            double[] array_to_reduce = new double[11];
            double[] reduced_array = new double[11];
            
            double new_cost = cost;
            
            for (int i = 0; i < 11; i++)
            {
                
                if (i == row) continue;
                
                for (int j = 0; j < 11; j++)
                {
                    
                    array_to_reduce[j] = array[i, j];
                }
                
                if (minimum(array_to_reduce) != 0)
                {
                    
                    new_cost = minimum(array_to_reduce) + new_cost;
                    
                    reduced_array = min(array_to_reduce, minimum(array_to_reduce));
                    
                    for (int k = 0; k < 11; k++)
                    {
                        array[i, k] = reduced_array[k];
                    }
                }
            }
           
            for (int i = 0; i < 11; i++)
            {
                
                if (i == column) continue;
                
                for (int j = 0; j < 11; j++)
                {
                    
                    array_to_reduce[j] = array[j, i];
                }
                
                if (minimum(array_to_reduce) != 0)
                {
                    
                    new_cost = minimum(array_to_reduce) + new_cost;
                    
                    reduced_array = min(array_to_reduce, minimum(array_to_reduce));
                    
                    for (int k = 0; k < 11; k++)
                    {
                        array[k, i] = reduced_array[k];
                    }
                }
            }
            
            return new_cost;
        }

        public static void expand(List<lobject> l, lobject o)
        {
            
            int length = o.remainingcity.Length;
            for (int i = 0; i < length; i++)
            {
               
                if (o.remainingcity[i] == 0) continue;
                double cost = 0;
                cost = o.cost;
                int city = o.city;
                Stack<double> st = new Stack<double>();

                for (int st_i = 0; st_i < o.st.Count; st_i++)
                {

                    double k = o.st.ElementAt(st_i);
                    //Console.WriteLine(k);
                    st.Push(k);
                }

                st.Push(o.remainingcity[i]);
                
                double[,] temparray = new double[11, 11];
                for (int i_1 = 0; i_1 < 5; i_1++)
                {
                    for (int i_2 = 0; i_2 < 5; i_2++)
                    {
                        temparray[i_1, i_2] = o.matrix[i_1, i_2];
                    }
                }
                
                cost = cost + temparray[city, o.remainingcity[i]];
                
                for (int j = 0; j < 11; j++)
                {
                    temparray[city, j] = 9999;
                    temparray[j, o.remainingcity[i]] = 9999;
                }
                
                temparray[o.remainingcity[i], 0] = 9999;
                
                double cost1 = reduce(temparray, cost, city, o.remainingcity[i]);
                
                lobject finall = new lobject(11);
                finall.city = o.remainingcity[i];
                finall.cost = cost1;
                finall.matrix = temparray;
                int[] temp_array = new int[o.remainingcity.Length];
                
                for (int i_3 = 0; i_3 < temp_array.Length; i_3++)
                {
                    temp_array[i_3] = o.remainingcity[i_3];
                }
                temp_array[i] = 0;
                finall.remainingcity = temp_array;
                finall.city_left_to_expand = o.city_left_to_expand - 1;
                finall.st = st;
                l.Add(finall);
            }
        }

        public static double[] decreasing_sort(double[] temp)
        {
            double[] y = new double[temp.Length];
            
            for (int j = 0; j < temp.Length; j++)
            {
                y[j] = temp[j];
            }
            double x = 0;
            
            for (int i = 0; i < temp.Length - 1; i++)
            {
                if (temp[i] < temp[i + 1])
                {
                    x = temp[i];
                    temp[i] = temp[i + 1];
                    temp[i + 1] = x;
                }
            }
            double[] to_be_returned = new double[temp.Length];
            int f = 0;
            
            for (int j = 0; j < temp.Length; j++)
            {
                for (int j1 = 0; j1 < temp.Length; j1++)
                {
                    if (temp[j] == y[j1])
                    {
                        to_be_returned[j] = j1;
                    }
                }
            }
            return to_be_returned;
        }


        static lobject temp_best_solution = new lobject(1);

        static void MethodOfBranchAndBoundary(long startPointId)
        {
            int NUMBER_CITIES = 11;

            double[,] array = new double[11, 11];
            int i = 0;
            int j = 0;
            idHospitals.Insert(0,startPointId);
            for(i = 0; i < 11; ++i)
            {
                for (j = 0; j < 11; ++j)
                {
                    if (i != j)
                    {
                        AStarAlgorithm(idHospitals[i], idHospitals[j], 1);
                        array[i, j] = AStarWeigth[1];
                    }
                    else
                    {
                        array[i, j] = 100000;
                    }
                }
            }

            
            double[,] maintemp = new double[NUMBER_CITIES, NUMBER_CITIES];
            double x = 0;
            x = reduce(array, x, 9999, 9999);
            lobject l1 = new lobject(NUMBER_CITIES);

            
            l1.city = 0;
            l1.cost = x;
            l1.matrix = array;
            l1.st.Push(0);
            l1.remainingcity = new int[NUMBER_CITIES - 1];
            l1.city_left_to_expand = NUMBER_CITIES - 1;

            for (int o = 0; o < 10; o++)
            {
                l1.remainingcity[o] = o + 1;
            }
            
            int count = 0;
            
            Stack<lobject> s = new Stack<lobject>();
            
            s.Push(l1);

            
            temp_best_solution = new lobject(NUMBER_CITIES);
            double current_best_cost = 1000000;
           
            while (s.Count != 0)
            {
                
                List<lobject> main = new List<lobject>();

                lobject hell = new lobject(NUMBER_CITIES);
                hell = s.Pop();
                //Console.WriteLine("{0}",s.Count);
               
                if (hell.city_left_to_expand == 0)
                {
                    
                    if (hell.cost <= current_best_cost)
                    {
                        temp_best_solution = hell;
                        current_best_cost = temp_best_solution.cost;
                    }
                }
                else if (hell.city_left_to_expand != 0)
                {
                    if (hell.cost <= current_best_cost)
                    {
                        count++;
                        
                        expand(main, hell);

                        
                        double[] arrow = new double[main.Count()];
                        for (int pi = 0; pi < main.Count(); pi++)
                        {
                            lobject help = (lobject)main.ElementAt(pi);
                            arrow[pi] = help.cost;
                        }
                        
                        double[] tempppp = decreasing_sort(arrow);
                        for (int pi = 0; pi < tempppp.Length; pi++)
                        {
                            
                            s.Push((lobject)main.ElementAt((int)tempppp[pi]));

                        }
                    }
                }
            }

            List<long> startPointAddList = new List<long>();
            startPointAddList = AddjestedList[idHospitals[(int)temp_best_solution.st.ElementAt(0)]];
            for (int k = 0; k < 10; ++k)
            {
                AStarAlgorithm(idHospitals[(int)temp_best_solution.st.ElementAt(k)], idHospitals[(int)temp_best_solution.st.ElementAt(k+1)],k);
                NeighborWays[k] = AStarWays[k];
                if ((int)temp_best_solution.st.ElementAt(k)==0)
                {
                    orderOfHospitals[0] = (int)temp_best_solution.st.ElementAt(k);
                    
                }
                AddjestedList.Remove(idHospitals[(int)temp_best_solution.st.ElementAt(k)]);
                

            }



            AddjestedList.Add(idHospitals[(int)temp_best_solution.st.ElementAt(0)], startPointAddList);
            AStarAlgorithm(idHospitals[(int)temp_best_solution.st.ElementAt(10)], idHospitals[(int)temp_best_solution.st.ElementAt(0)], 10);
            NeighborWays[10] = AStarWays[10];
            idHospitals.RemoveAt(0);



        }




        static void Main(string[] args)
        {
            string path;
            Console.WriteLine("Введите полный путь необходимого для обработки osm файла:");
            path = Console.ReadLine();
            ParseOsm(path);
            AddjestedList_for_output = AddjestedList;
            Console.WriteLine("Введите значение LAT для точки, от которой мы будем искать путь в задаче коммивояжера(значение от {0} до {1})", minlat, maxlat);
            double lat = Double.Parse(Console.ReadLine());
            Console.WriteLine("Введите значение LON для точки, от которой мы будем искать путь в задаче коммивояжера(значение от {0} до {1})", minlon, maxlon);
            double lon = Double.Parse(Console.ReadLine());
            long startPointId = findNearestPointToCoordinates(lat, lon);

            long lastPointId = startPointId;


            Console.WriteLine("Введите 1 для решения задачи коммивояжера методом ближайшего соседа");
            Console.WriteLine("Введите 2 для решения задачи коммивояжера методом ветвей и границ");
            int type = int.Parse(Console.ReadLine());
            if (type == 1)
            {
                neighborMethod(startPointId);
            }
            else
            {
                MethodOfBranchAndBoundary(startPointId);
            }

            


            WriteSvg(startPointId);
            //Console.ReadLine();

        }
    }
}
