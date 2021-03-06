using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Parser
{
    public abstract class Closure : IEnumerable, ICloneable
    {
        public string Title { get; set; }

        public Grammar Grammar { get; protected set; }

        public List<SLRProduction> SLRProductions { get; protected set; }

        protected Closure(String title, Grammar grammar)
        {
            Title = title;
            Grammar = grammar;
            SLRProductions = new List<SLRProduction>();
        }

        [IndexerName("SLRProduction")]
        public SLRProduction this[int index]
        {
            get { return SLRProductions[index]; }
        }

        public int Count
        {
            get { return (SLRProductions == default(List<SLRProduction>)) ? 0 : SLRProductions.Count; }
        }

        public bool IsNull
        {
            get { return Count == 0; }
        }

        public EntityCollection<Terminal> Terminals
        {
            get
            {
                //var arrTerms = default(EntityCollection<Terminal>);
                //foreach (var production in this)
                //{
                //    foreach (var entity in production.Product)
                //    {
                //        //if (entity is Terminal)
                //        {
                //            arrTerms += entity as Terminal;
                //        }
                //    }
                //}

                var arrTerms = this.Cast<Production>().SelectMany(production => production.Product).Aggregate(default(EntityCollection<Terminal>), (current, entity) => current + (entity as Terminal));
                return arrTerms.RemoveRedundancy() as EntityCollection<Terminal>;
            }
        }

        public EntityCollection<NonTerminal> NonTerminals
        {
            get
            {
                //var arrNonTerms = default(EntityCollection<NonTerminal>);
                //foreach (var production in this)
                //{
                //    foreach (var entity in production.Product)
                //    {
                //        //if (entity is NonTerminal)
                //        {
                //            arrNonTerms += entity as NonTerminal;
                //        }
                //    }
                //}
                var arrNonTerms = this.Cast<Production>().SelectMany(production => production.Product).Aggregate(default(EntityCollection<NonTerminal>), (current, entity) => current + (entity as NonTerminal));
                return arrNonTerms.RemoveRedundancy() as EntityCollection<NonTerminal>;
            }
        }

        public EntityCollection<Entity> Entities
        {
            get
            {
                //var entityCol = default(EntityCollection<Entity>);
                //foreach (var production in this)
                //{
                //    foreach (var entity in production.Product) 
                //        entityCol += entity;
                //}
                var entityCol = this.Cast<Production>().SelectMany(production => production.Product).Aggregate(default(EntityCollection<Entity>), (current, entity) => current + entity);
                return entityCol.RemoveRedundancy() as EntityCollection<Entity>;
            }
        }

        #region ICloneable Members

        public abstract Object Clone();

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return SLRProductions.GetEnumerator();
        }

        #endregion

        public void Add(SLRProduction production)
        {
            if (production != default(CLRProduction)) SLRProductions.Add(production);
        }

        public void AddRange(IEnumerable<SLRProduction> productions)
        {
            if (productions != default(IEnumerable<CLRProduction>)) SLRProductions.AddRange(productions);
        }

        public int FindFirstIndex(SLRProduction find, int startIndex)
        {
            return SLRProductions.FindIndex(startIndex, production => (production == find));
        }

        public int FindFirstIndex(CLRProduction find)
        {
            return FindFirstIndex(find, 0);
        }

        public void RemoveAt(int index)
        {
            SLRProductions.RemoveAt(index);
        }

        public void Remove(CLRProduction production)
        {
            RemoveAt(FindFirstIndex(production));
        }

        public void RemoveRedundancy()
        {
            var count = Count - 1;
            for (var index = 0; index < count;)
            {
                var findIdx = FindFirstIndex(this[index], index + 1);
                if (findIdx != -1)
                {
                    RemoveAt(findIdx);
                    --count;
                    continue;
                }
                ++index;
            }
        }

        public SLRProduction[] ToArray()
        {
            return SLRProductions.ToArray();
        }

        public bool Equals(Closure closure)
        {
            return this == closure;
        }

        public bool NotEquals(Closure closure)
        {
            return this != closure;
        }

        public static bool operator ==(Closure closure1, Closure closure2)
        {
            if (ReferenceEquals(closure1, closure2)) return true;
            if (null == (Object) closure1 || null == (Object) closure2) return false;
            var count1 = closure1.Count;
            var count2 = closure2.Count;
            if (count1 != count2) return false;

            if (closure1.Grammar != closure2.Grammar) return false;
            for (int index = 0; index < count1; ++index) if (closure1[index] != closure2[index]) return false;
            return true;
        }

        public static bool operator !=(Closure closure1, Closure closure2)
        {
            return !(closure1 == closure2);
        }

        public override bool Equals(Object obj)
        {
            return (obj is Closure) ? Equals(obj as Closure) : base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override String ToString()
        {
            var sbClosure = new StringBuilder(Title);
            sbClosure.AppendLine();
            foreach (SLRProduction production in this)
            {
                sbClosure.Append(production);
                sbClosure.AppendLine();
            }
            sbClosure.Remove(sbClosure.Length - 1, 1);
            return sbClosure.ToString();
        }

        public abstract Closure GoToEntity(Entity X);
    }

    internal class SLRClosure : Closure
    {
        public SLRClosure(String title, Grammar grammar, IEnumerable<SLRProduction> productLR)
            : base(title, grammar)
        {
            if (productLR == default(SLRProduction[])) return;

            SLRProductions = new List<SLRProduction>(productLR);
            var lastB = default(NonTerminal);
            //A --> α.Bβ, a
            for (var index = 0; index < Count; ++index)
            {
                var product = this[index];
                if (product.DotPosition != product.Count)
                {
                    var entity = product.Product[product.DotPosition];

                    if (entity is NonTerminal)
                    {
                        var B = entity as NonTerminal; //B Non-Terminal
                        if (B != lastB)
                        {
                            //B --> .rX, b
                            foreach (Production production in grammar.Productions) if (production.Producer == B) Add(new SLRProduction(B, production.Product)); // gamma
                            lastB = B;
                        }
                    }
                }
            }
            RemoveRedundancy();
        }

        public SLRClosure(String title, Grammar grammar)
            : base(title, grammar) {}

        public SLRClosure()
            : this(default(String), new Grammar(), new[] {new SLRProduction()}) {}

        public SLRClosure(String title, Grammar grammar, SLRClosure closure)
            : base(title, grammar)
        {
            SLRProductions = (closure == default(SLRClosure)) ? default(List<SLRProduction>) : closure.SLRProductions;
        }

        public override Closure GoToEntity(Entity X)
        {
            var closure = new SLRClosure("gotoX", Grammar);
            for (var index = 0; index < Count; ++index)
            {
                var product = this[index];
                var dot = product.DotPosition;
                if (dot != product.Count)
                {
                    var Y = product.Product[dot];
                    if (Y == X) closure.Add(new SLRProduction(product.Producer, product.Product, dot + 1));
                }
            }
            return closure;
        }

        public static explicit operator SLRProduction[](SLRClosure closure)
        {
            return closure.ToArray();
        }

        public override Object Clone()
        {
            return new SLRClosure(Title, Grammar, new List<SLRProduction>(SLRProductions));
        }
    }

    internal class CLRClosure : SLRClosure
    {
        #region Constructors

        public CLRClosure(String title, Grammar grammar, IEnumerable<SLRProduction> productLALR)
            : base(title, grammar)
        {
            SLRProductions = ((productLALR == default(CLRProduction[]))
                                ? new List<SLRProduction>(new CLRProduction[] {})
                                : new List<SLRProduction>(productLALR));
            var lastB = default(NonTerminal);
            //A --> α.Bβ, a
            for (var index = 0; index < Count; ++index)
            {
                var product = this[index] as CLRProduction;
                if (product.DotPosition != product.Count)
                {
                    var entity = product.Product[product.DotPosition];
                    if (entity is NonTerminal)
                    {
                        var B = entity as NonTerminal; //B Non-Terminal
                        if (B != lastB)
                        {
                            var beta = product.Product.GetRange(product.DotPosition + 1); //β Next Entity to B
                            var a = product.LookAheads; //a Look Aheads
                            //B --> .rX, b
                            foreach (Production production in grammar.Productions)
                            {
                                if (production.Producer == B)
                                {
                                    //b = First(βa);
                                    var b = a.Aggregate(default(EntityCollection<Terminal>), (current, term) => current + grammar.First(beta + term));
                                    b =
                                        (EntityCollection<Terminal>)
                                        (b != default(EntityCollection<Terminal>)
                                             ? b.RemoveRedundancy()
                                             : new EntityCollection<Terminal>(new[] {(Terminal) "#"}));

                                    //Adding B --> .rX, b
                                    Add(new CLRProduction(B, production.Product, b)); // gamma
                                }
                            }
                            lastB = B;
                        }
                    }
                }
            }
            RemoveRedundancy();
        }

        public CLRClosure(String title, Grammar grammar)
            : base(title, grammar) {}

        public CLRClosure()
            : this(default(String), new Grammar(), new SLRProduction[] {new CLRProduction()}) {}

        public CLRClosure(String title, Grammar grammar, CLRClosure closure)
            : base(title, grammar, closure)
        {
            //_productions = (closure == default(LALRClosure)) ? default(List<LRProduction>) : new List<LRProduction>(closure.ToArray());
        }

        #endregion

        public void AddLookAhead(SLRProduction[] prodLALR)
        {
            for (var index = 0; index < Count; ++index)
            {
                var prod1 = this[index] as CLRProduction;
                var prod2 = prodLALR[index] as CLRProduction;

                //foreach (Terminal lookahead in prod2.LookAheads)
                prod1.LookAheads.List.AddRange(prod2.LookAheads);
                prod1 = prod1.LookAheads.RemoveRedundancy() as CLRProduction;
            }
        }

        public override Closure GoToEntity(Entity X)
        {
            var closure = new CLRClosure("gotoX", Grammar);
            for (var index = 0; index < Count; ++index)
            {
                var prodLALR = this[index] as CLRProduction;
                var dot = prodLALR.DotPosition;
                if (dot != prodLALR.Count)
                {
                    var Y = prodLALR.Product[dot];
                    if (Y == X) closure.Add(new CLRProduction(prodLALR.Producer, prodLALR.Product, dot + 1, prodLALR.LookAheads));
                }
            }
            return closure;
        }

        public static explicit operator CLRProduction[](CLRClosure closure)
        {
            return (CLRProduction[]) closure.ToArray();
        }

        public override Object Clone()
        {
            return new SLRClosure(Title, Grammar, new List<SLRProduction>(SLRProductions));
        }
    }
}