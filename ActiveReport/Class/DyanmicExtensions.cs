using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ActiveReport.Class
{
    public static class DyanmicExtensions
    {
        public static XElement ToXml(this object obj, string rootName, string tableName)
        {
            var resultXml = new XElement(rootName);

            foreach (var result in (dynamic)obj)
            {
                var item = new XElement(tableName);

                foreach (var kvp in result)
                {
					try
					{
						item.AddFirst(new XElement(kvp.Key, kvp.Value));
					}
					catch (Exception)
					{
						item.AddFirst(new XElement(String.Concat("UnknownTagOnSQLScript_", Guid.NewGuid().ToString()), kvp.Value));

					}
					
                }

                resultXml.AddFirst(item);
            }

            return resultXml;
        }
    }
}
