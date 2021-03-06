using System.Linq;

namespace Parser
{
    using System;
    using System.Text;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Collections.ObjectModel;
    using System.IO;

    public sealed class Grammar : Collection<Production>
    {
        #region Constructors
        public Grammar()
            : base()
        { }

        public Grammar(IList<Production> list)
            : base(list)
        { }

        public Grammar(Grammar grammar)
            : base(grammar.Items)
        { }
        #endregion

        public List<Production> Productions
        {
            get { return (List<Production>) Items; }
        }

        public void AddRange(IEnumerable<Production> productions)
        {
            if (default(IEnumerable<Production>) != productions)
            {
                foreach (var production in productions)
                {
                    Add(production);
                }
            }
        }

        public int Search(Production find, int startIndex)
        {
            return ((List<Production>) Items).FindLastIndex(startIndex, production => (production == find));
        }

        public int Search(Production production) { return Search(production, Count - 1); }

        public EntityCollection<Terminal> Terminals
        {
            get
            {
                var defaultEntityCol = default(EntityCollection<Terminal>);
                //var termCol = defaultEntityCol;
                //foreach (var production in this)
                //{
                //    foreach (var entity in production.Product)
                //    {
                //        if (entity is Terminal)
                //        {
                //            var terminal = entity as Terminal;
                //            termCol += terminal;
                //        }
                //    }
                //}

                var termCol = this.SelectMany(production => production.Product).OfType<Terminal>().Aggregate(defaultEntityCol, (current, terminal) => current + terminal);
                
                return (termCol != defaultEntityCol)
                           ? termCol.RemoveRedundancy() as EntityCollection<Terminal>
                           : defaultEntityCol;
            }
        }

        public EntityCollection<NonTerminal> NonTerminals
        {
            get
            {
                var defaultEntityCol = default(EntityCollection<NonTerminal>);
                var nontermCol = defaultEntityCol;
                foreach (var production in this)
                {
                    nontermCol += production.Producer;

                    //foreach (var entity in production.Product)
                    //    if (entity is NonTerminal)
                    //    {
                    //        var nonterminal = entity as NonTerminal;
                    //        nontermCol += nonterminal;
                    //    }

                    nontermCol = production.Product.OfType<NonTerminal>().Aggregate(nontermCol, (current, nonterminal) => current + nonterminal);
                }
                return (nontermCol != defaultEntityCol) ?
                    nontermCol.RemoveRedundancy() as EntityCollection<NonTerminal> :
                    defaultEntityCol;
            }
        }

        public EntityCollection<Entity> Entities
        {
            get
            {
                var defaultEntityCol = default(EntityCollection<Entity>);
                var entityCol = defaultEntityCol;
                foreach (var production in this)
                {
                    entityCol += production.Producer;
                    //foreach (var entity in production.Product)
                    //    entityCol += entity;
                    entityCol = production.Product.Aggregate(entityCol, (current, entity) => current + entity);
                }
                return (entityCol != defaultEntityCol)
                           ? entityCol.RemoveRedundancy() as EntityCollection<Entity>
                           : defaultEntityCol;
            }
        }

        #region First & Follow

        public EntityCollection<Terminal> First(EntityCollection<Entity> product)
        {
            var defaultEntityCol = default(EntityCollection<Terminal>);
            if (product == defaultEntityCol) return defaultEntityCol;
            // product = [AB...$]
            var A = product[0];

            var terminal = A as Terminal;
            if (terminal != null)
            {
                return (A.Title != "#")
                           ? new EntityCollection<Terminal>(new[] {terminal})
                           : (product.Count > 1)
                                 ? First(product.GetRange(1))
                                 : defaultEntityCol;
            }
            //entitySet[0] is NonTerminal 
            EntityCollection<Terminal> producerFirst = defaultEntityCol;
            foreach (Production production in this)
            {
                if ((production.Producer == A)
                 && (production.Product[0] != A)) // Left recursion
                {
                    var firsts = First(production.Product);
                    if (firsts != defaultEntityCol)
                    {
                        producerFirst += firsts;
                    }
                    else
                    {
                        producerFirst += (Terminal) "#";
                    }
                    //producerFirst += (firsts != defaultEntityCol) ? firsts : (Terminal) "#";
                }
            }
            return (producerFirst != defaultEntityCol) ?
                producerFirst.RemoveRedundancy() as EntityCollection<Terminal> :
                producerFirst;
        }

        private EntityCollection<Terminal> Follow(Entity B, EntityCollection<Entity> exclude)
        {
            EntityCollection<Terminal> defaultEntityCol = default(EntityCollection<Terminal>);
            EntityCollection<Terminal> followB = defaultEntityCol;

            foreach (Production production in this)
            {
                //A --> Bβ
                NonTerminal A = production.Producer;
                EntityCollection<Entity> product = production.Product;

                var findIdx = product & B;
                while (findIdx != -1)
                {
                    var beta = product.GetRange(findIdx + 1);
                    var firstBeta = First(beta);
                    if (firstBeta == defaultEntityCol)
                    {
                        if ((beta == defaultEntityCol || (beta & A) == -1)
                         && (exclude == defaultEntityCol || (exclude & A) == -1))
                        {
                            followB += Follow(A, exclude + A); //Follow(B) = Follow(A)
                        }
                        break;
                    }

                    followB += firstBeta;

                    if ((firstBeta & (Terminal) "#") != -1)
                    {
                        if (exclude == defaultEntityCol || (exclude & A) == -1)
                        {
                            followB += Follow(A, exclude + A); //Follow(B) = Follow(A)
                        }
                    }
                    var index1 = beta & B;
                    var index2 = (index1 == -1) ? 0 : findIdx + 1;
                    findIdx = index1 + index2;
                }
            }

            if (followB != defaultEntityCol)
            {
                followB -= (Terminal) "#";
                followB = followB.RemoveRedundancy() as EntityCollection<Terminal>;
            }
            return followB;
        }

        public EntityCollection<Terminal> Follow(Entity B) { return Follow(B, B); }

        #endregion

        #region Static Methods

        public static implicit operator Grammar(Production[] arrProduction) { return new Grammar(new List<Production>(arrProduction)); }

        public static Grammar operator +(Grammar grammar, Production production)
        {
            if (default(Grammar) == grammar) return new Grammar(new[] { production });
            if (default(Production) == production) return grammar;
            var combine = new Grammar();
            combine.AddRange(grammar.Productions);
            combine.Add(production);
            return combine;
        }

        public static Grammar operator +(Grammar grammar1, Grammar grammar2)
        {
            if (grammar1 == default(Grammar)) return grammar2;
            if (grammar2 == default(Grammar)) return grammar1;
            var combine = new Grammar();
            combine.AddRange(grammar1.Productions);
            combine.AddRange(grammar2.Productions);
            return combine;
        }

        public static Grammar operator -(Grammar grammar1, Grammar grammar2)
        {
            if (grammar1 == default(Grammar)) return default(Grammar);
            if (grammar2 == default(Grammar)) return new Grammar(grammar1);

            var nRemoved = 0;
            var remove = new Grammar(grammar1);
            foreach (var production in grammar2)
            {
                var index = grammar1.Search(production, nRemoved);
                if (index != -1)
                {
                    remove.RemoveAt(index - nRemoved);
                    ++nRemoved;
                }
            }
            return remove;
        }

        public static int operator &(Grammar grammar, Production find)
        {
            return grammar.Search(find);
        }

        public static bool operator ==(Grammar grammar1, Grammar grammar2)
        {
            if (ReferenceEquals(grammar1, grammar2)) return true;
            if (ReferenceEquals(null, grammar1) || ReferenceEquals(null, grammar2)) return false;

            int length1 = grammar1.Count;
            int length2 = grammar2.Count;
            if (length1 != length2) return false;

            for (int index = 0; index < length1; ++index)
            {
                if (grammar1[index] != grammar2[index])
                {
                    return false;
                }
            }
            return true;
        }

        public static bool operator !=(Grammar grammar1, Grammar grammar2)
        {
            return !(grammar1 == grammar2);
        }
        #endregion

        public bool Equals(Grammar grammar) { return this == grammar; }

        public bool NotEquals(Grammar grammar) { return this != grammar; }

        public bool Equals(params Production[] arrProduction) { return Equals((Grammar) arrProduction); }

        #region Overrided Methods

        public override bool Equals(Object obj)
        {
            if (!(obj is Grammar)) return false;
            return this == (Grammar) obj;
        }
        public override int GetHashCode()
        {
            return ToString().GetHashCode() ^ base.GetHashCode();
        }

        public override String ToString()
        {
            var strBuild = new StringBuilder();
            var index = 0;
            foreach (Production product in this)
            {
                strBuild.AppendFormat("({0:0#}) ", index++);
                strBuild.Append(product);
                strBuild.AppendLine();
            }
            strBuild.Remove(strBuild.Length - 1, 1);
            return strBuild.ToString();
        }
        #endregion

        #region IO Methods

        private const String HEADER = "[Grammar]";
        private const String SEPARATOR = "-->";
        private const String FOOTER = "[End]";

        public static Grammar Read(String filename)
        {
            int lineNum = 0;
            try
            {
                using (var reader = new StreamReader(new FileStream(filename, FileMode.Open)))
                {
                    var line = ReadLine(reader, ref lineNum);

                    if (String.IsNullOrEmpty(line) || !line.Equals(HEADER)) throw new FormatException("Header Missing - " + HEADER);

                    var grammar = default(Grammar);

                    while (true)
                    {
                        if (reader.EndOfStream) throw new FormatException("Footer Missing - " + FOOTER);

                        line = ReadLine(reader, ref lineNum);

                        if (String.IsNullOrEmpty(line)) continue;
                        if (line.Equals(FOOTER)) break;


                        grammar += ReadProduction(line);
                        if (default(Grammar) == grammar) continue;
                        // To code next
                    }
                    reader.Close();
                    return grammar;
                }
            }
            catch (FormatException expFormat)
            {
                throw new FormatException("Line# : " + lineNum + "\nException : " + expFormat.Message);
            }
        }

        public static Grammar Read(params String[] grmProductions)
        {
            Grammar grammar = default(Grammar);
            for (var index = 0; index < grmProductions.Length; ++index)
            {
                try
                {
                    grammar += ReadProduction(grmProductions[index]);
                }
                catch (FormatException expFormat)
                {
                    throw new FormatException("Line# : " + index + "\nException : " + expFormat.Message);
                }
            }
            return grammar;
        }

        public static void Write(Grammar grammar, String filename)
        {
            using (var writer = new StreamWriter(new FileStream(filename, FileMode.Create)))
            {
                writer.WriteLine(HEADER);
                writer.WriteLine(grammar);
                writer.WriteLine(FOOTER);
            }
        }

        private static Production ReadProduction(String line)
        {
            var producer = default(NonTerminal);
            var product = default(EntityCollection<Entity>);

            int index;
            var entity = default(String);
            do
            {
                if (String.IsNullOrEmpty(line)) break;
                index = line.IndexOf(" ", StringComparison.Ordinal);
                if (index == -1)
                    throw new FormatException("(Space Missing) " + line);

                entity = line.Substring(0, index);
                line = line.Substring(index + 1);
            }
            while (String.IsNullOrEmpty(entity));

            if (String.IsNullOrEmpty(line) || String.IsNullOrEmpty(entity)) return default(Production);

            if (!char.IsUpper(entity[0]))
                throw new FormatException("(Must be UpperCase) " + entity[0] + " " + line);

            producer = (NonTerminal) entity;

            do
            {
                index = line.IndexOf(" ", StringComparison.Ordinal);
                if (index == -1)
                    throw new FormatException("(Space Missing) " + line);

                entity = line.Substring(0, index);
                line = line.Substring(index + 1);
            }
            while (String.IsNullOrEmpty(entity));

            if (!entity.Equals(SEPARATOR))
                throw new FormatException("(" + SEPARATOR + " Missing) " + entity + " " + line);

            while (!String.IsNullOrEmpty(line))
            {
                do
                {
                    index = line.IndexOf(" ", StringComparison.Ordinal);
                    if (index == -1)
                    {
                        index = line.IndexOf("\n", StringComparison.Ordinal);
                        entity = (index != -1) ? line.Substring(0, index) : line.Substring(0);
                        line = String.Empty;
                        break;
                    }
                    entity = line.Substring(0, index);
                    line = line.Substring(index + 1);
                }
                while (String.IsNullOrEmpty(entity));

                if (String.IsNullOrEmpty(entity)) continue;
                if (char.IsUpper(entity[0]))
                    product += (NonTerminal) entity;
                else//if (char.IsLower(entity[0]))
                    product += (Terminal) entity;
            }
            return new Production(producer, product);
        }

        private static String ReadLine(StreamReader reader, ref int lineNum)
        {
            ++lineNum;
            return reader.ReadLine();
        }
        #endregion
    }
}