using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace Parser
{
    public sealed class Grammar : Collection<Production>, ISet
    {
        public Grammar() { }

        public Grammar(IList<Production> list)
            : base(list) { }

        public Grammar(Grammar grammar)
            : base(grammar.Items) { }

        public List<Production> Productions
        {
            get { return Items as List<Production>; }
        }

        public EntityCollection<Terminal> Terminals
        {
            get
            {
                var defaultEntityCol = default(EntityCollection<Terminal>);
                var termCol = defaultEntityCol;
                foreach (var production in this)
                    foreach (var entity in production.Product)
                    {
                        if (!(entity is Terminal)) continue;

                        var terminal = entity as Terminal;
                        termCol += terminal;
                    }
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
                    foreach (var entity in production.Product)
                    {
                        if (!(entity is NonTerminal)) continue;

                        var nonterminal = entity as NonTerminal;
                        nontermCol += nonterminal;
                    }
                }
                return (nontermCol != defaultEntityCol)
                           ? nontermCol.RemoveRedundancy() as EntityCollection<NonTerminal>
                           : defaultEntityCol;
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
                    foreach (var entity in production.Product)
                        entityCol += entity;
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
            if (product == defaultEntityCol || product.Count == 0) return defaultEntityCol;
            // product = [AB...$]
            var A = product[0];

            var terminal = A as Terminal;
            if (null != terminal)
                return (terminal.Title != "#")
                           ? new EntityCollection<Terminal>(new[] { terminal })
                           : (product.Count > 1)
                                 ? First(product.GetRange(1))
                                 : defaultEntityCol;

            //entitySet[0] is NonTerminal 
            var producerFirst = defaultEntityCol;
            foreach (var production in this)
            {
                if ((production.Producer != A) || (production.Product[0] == A)) continue;

                var firsts = First(production.Product);

                //if (firsts != defaultEntityCol)
                //    producerFirst += firsts;
                //else
                //    producerFirst += (Terminal)"#";
                producerFirst += (firsts != defaultEntityCol)
                                     ? firsts
                                     : (EntityCollection<Terminal>) ((Terminal) "#");
            }

            return (producerFirst != defaultEntityCol)
                       ? producerFirst.RemoveRedundancy() as EntityCollection<Terminal>
                       : producerFirst;
        }

        EntityCollection<Terminal> Follow(Entity B, EntityCollection<Entity> exclude)
        {
            var defaultEntityCol = default(EntityCollection<Terminal>);
            if (default(Entity) == B) return defaultEntityCol;
            var followB = defaultEntityCol;

            foreach (var production in this)
            {
                //A --> Bβ
                var A = production.Producer;
                var product = production.Product;

                var idxEnt = product & B;
                while (-1 != idxEnt)
                {
                    var beta = product.GetRange(idxEnt + 1);
                    var firstBeta = First(beta);
                    if (firstBeta == defaultEntityCol)
                    {
                        if ((beta == defaultEntityCol || (beta & A) == -1)
                            && (exclude == defaultEntityCol || (exclude & A) == -1))
                            followB += Follow(A, exclude + A); //Follow(B) = Follow(A)

                        break;
                    }
                    followB += firstBeta;

                    if ((firstBeta & (Terminal) "#") != -1)
                        if (exclude == defaultEntityCol || (exclude & A) == -1)
                            followB += Follow(A, exclude + A); //Follow(B) = Follow(A)

                    var index1 = beta & B;
                    var index2 = (index1 == -1) ? 0 : idxEnt + 1;

                    idxEnt = index1 + index2;
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

        public void AddRange(IEnumerable<Production> productions)
        {
            if (null == productions) return;
            foreach (var production in productions) Add(production);
        }

        public bool Equals(Grammar grammar) { return (this == grammar); }

        public bool NotEquals(Grammar grammar)
        {
            return !Equals(grammar); //(this != grammar);
        }

        public bool Equals(params Production[] arrProduction) { return Equals(new Grammar(arrProduction)); }

        public bool NotEquals(params Production[] arrProduction) { return !Equals(arrProduction); }

        #region Overrided Methods

        public override bool Equals(Object obj) { return (obj is Grammar) ? Equals(obj as Grammar) : base.Equals(obj); }

        public override int GetHashCode() { return ToString().GetHashCode() ^ base.GetHashCode(); }

        public override String ToString()
        {
            var sb = new StringBuilder();
            var index = 0;
            foreach (var product in this)
            {
                sb.AppendFormat("({0:0#}) ", index++);
                sb.Append(product);
                sb.AppendLine();
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        #endregion

        #region ISet Members

        public ISet RemoveRedundancy()
        {
            for (var i = Count - 1; i >= 0; --i)
            {
                var index = Productions.IndexOf(this[i], i + 1);
                if (index == -1) continue;

                RemoveAt(index);
            }

            //var uniqueStore = new Dictionary<int, Production>();
            //for (var index = Count - 1; index >= 0; --index)
            //{
            //    var value = Productions[index];
            //    if (uniqueStore.ContainsValue(value))
            //        Remove(value);
            //    else
            //        uniqueStore.Add(index, value);
            //}

            return this;
        }

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
            if (default(Grammar) == grammar1) return grammar2;
            if (default(Grammar) == grammar2) return grammar1;

            var combine = new Grammar();
            combine.AddRange(grammar1.Productions);
            combine.AddRange(grammar2.Productions);
            return combine;
        }

        public static Grammar operator -(Grammar grammar1, Grammar grammar2)
        {
            if (default(Grammar) == grammar1) return default(Grammar);
            if (default(Grammar) == grammar2) return new Grammar(grammar1);

            var common = 0;
            var remove = new Grammar(grammar1);
            foreach (var production in grammar2)
            {
                var index = grammar1.Productions.LastIndexOf(production, common);
                if (index == -1) continue;

                remove.RemoveAt(index - common);
                ++common;
            }
            return remove;
        }

        public static int operator &(Grammar grammar, Production prod)
        {
            return
                //grammar.Productions.LastIndexOf(prod);
                grammar.IndexOf(prod);
        }

        public static bool operator ==(Grammar grammar1, Grammar grammar2)
        {
            if (ReferenceEquals(grammar1, grammar2)) return true;
            if (ReferenceEquals(null, grammar1) || ReferenceEquals(null, grammar2)) return false;

            if (grammar1.Count != grammar2.Count) return false;

            for (var index = 0; index < grammar1.Count; ++index)
                if (grammar1[index] != grammar2[index])
                    return false;
            return true;
        }

        public static bool operator !=(Grammar grammar1, Grammar grammar2) { return !(grammar1 == grammar2); }

        #region IO Methods

        const String HEADER = "[Grammar]";
        const String SEPARATOR = "-->";
        const String FOOTER = "[End]";

        public static Grammar Read(String filename)
        {
            const Grammar defaultGrammar = default(Grammar);
            var grammar = defaultGrammar;
            var numLine = 0;
            try
            {
                using (var stream = new FileStream(filename, FileMode.Open))
                using (var reader = new StreamReader(stream))
                {
                    var line = ReadLine(reader);
                    ++numLine;
                    if (String.IsNullOrEmpty(line) || !line.Equals(HEADER)) throw new FormatException("Header Missing - " + HEADER);

                    while (true)
                    {
                        if (reader.EndOfStream) throw new FormatException("Footer Missing - " + FOOTER);

                        line = ReadLine(reader);
                        ++numLine;
                        if (String.IsNullOrEmpty(line)) continue;

                        if (line.Equals(FOOTER)) break;

                        grammar += ReadProduction(line);
                        //if (defaultGrammar == grammar) continue;
                    }

                    reader.Close();
                    stream.Close();
                }
            }
            catch (FormatException expFormat)
            {
                throw new FormatException("Line# : " + numLine + "\nException : " + expFormat.Message);
            }
            return (defaultGrammar != grammar)
               ? grammar.RemoveRedundancy() as Grammar
               : defaultGrammar;

        }

        public static Grammar Read(params String[] grmProductions)
        {
            const Grammar defaultGrammar = default(Grammar);
            var grammar = defaultGrammar;
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
            return (defaultGrammar != grammar)
                       ? grammar.RemoveRedundancy() as Grammar
                       : defaultGrammar;
        }

        public static void Write(Grammar grammar, String filename)
        {
            using (var stream = new FileStream(filename, FileMode.Create))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(HEADER);
                writer.WriteLine(grammar);
                writer.WriteLine(FOOTER);
            }
        }

        static Production ReadProduction(String line)
        {
            var producer = default(NonTerminal);
            var product = default(EntityCollection<Entity>);

            var entity = default(String);
            do
            {
                if (String.IsNullOrEmpty(line)) break;

                var index = line.IndexOf(" ", StringComparison.Ordinal);
                if (-1 == index) throw new FormatException("(Space Missing) " + line);

                entity = line.Remove(index);
                line = line.Substring(index + 1);
            } while (String.IsNullOrEmpty(entity));

            if (String.IsNullOrEmpty(line) || String.IsNullOrEmpty(entity)) return default(Production);

            if (!Char.IsUpper(entity[0])) throw new FormatException("(Must be UpperCase) " + entity[0] + " " + line);

            producer = (NonTerminal) entity;
            do
            {
                if (String.IsNullOrEmpty(line)) break;
                var index = line.IndexOf(" ", StringComparison.Ordinal);
                if (-1 == index) throw new FormatException("(Space Missing) " + line);

                entity = line.Remove(index);
                line = line.Substring(index + 1);
            } while (String.IsNullOrEmpty(entity));

            if (!entity.Equals(SEPARATOR)) throw new FormatException("(" + SEPARATOR + " Missing) " + entity + " " + line);

            while (!String.IsNullOrEmpty(line))
            {
                do
                {
                    var index = line.IndexOf(" ", StringComparison.Ordinal);
                    if (-1 == index)
                    {
                        index = line.IndexOf("\n", StringComparison.Ordinal);
                        entity = (index != -1) ? line.Remove(index) : line;
                        line = String.Empty;
                        break;
                    }
                    entity = line.Remove(index);
                    line = line.Substring(index + 1);
                } while (String.IsNullOrEmpty(entity));

                if (String.IsNullOrEmpty(entity)) continue;

                product += Char.IsUpper(entity[0])
                               ? new EntityCollection<Entity>((NonTerminal) entity)
                               : (Terminal) entity;
            }
            return new Production(producer, product);
        }

        static String ReadLine(TextReader reader)
        {
            if (default(TextReader) == reader) return null;
            var readLine = reader.ReadLine();
            return (default(String) != readLine)
                       ? readLine.Trim()
                       : null;
        }

        #endregion

        #endregion
    }
}