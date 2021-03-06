using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Parser
{
    public abstract class Closure : IEnumerable, ICloneable
    {
        public string Title { get; set; }

        public Grammar Grammar { get; protected set; }

        public List<SLRProduction> SLRProductions { get; protected set; }

        protected Closure(String title, Grammar grammar, List<SLRProduction> productions = default(List<SLRProduction>))
        {
            Title = title;
            Grammar = grammar;
            SLRProductions = productions ?? new List<SLRProduction>();
        }

        protected Closure(String title, Grammar grammar)
            : this(title, grammar, new List<SLRProduction>()) { }

        [IndexerName("SLRProduction")]
        public SLRProduction this[int index]
        {
            get { return SLRProductions[index]; }
        }

        public int Count
        {
            get { return (SLRProductions == default(List<SLRProduction>)) ? 0 : SLRProductions.Count; }
        }

        public bool IsEmpty
        {
            get { return (Count == 0); }
        }

        public EntityCollection<Terminal> Terminals
        {
            get
            {
                var arrTerms = default(EntityCollection<Terminal>);
                foreach (Production production in this) 
                    foreach (var entity in production.Product) 
                        if (entity is Terminal) arrTerms += entity as Terminal;
                return (null != arrTerms)
                           ? arrTerms.RemoveRedundancy() as EntityCollection<Terminal>
                           : null;
            }
        }

        public EntityCollection<NonTerminal> NonTerminals
        {
            get
            {
                var arrNonTerms = default(EntityCollection<NonTerminal>);
                foreach (Production production in this) 
                    foreach (var entity in production.Product)
                        if (entity is NonTerminal) arrNonTerms += entity as NonTerminal;
                return (null != arrNonTerms)
                           ? arrNonTerms.RemoveRedundancy() as EntityCollection<NonTerminal>
                           : null;
            }
        }

        public EntityCollection<Entity> Entities
        {
            get
            {
                var entityCol = default(EntityCollection<Entity>);
                foreach (Production production in this)
                    foreach (var entity in production.Product) entityCol += entity;
                return (null != entityCol)
                           ? entityCol.RemoveRedundancy() as EntityCollection<Entity>
                           : null;
            }
        }

        public void Add(SLRProduction production)
        { if (default(Production) != production) SLRProductions.Add(production); }

        public void AddRange(IEnumerable<SLRProduction> productions)
        { if (default(IEnumerable<SLRProduction>) != productions) SLRProductions.AddRange(productions); }

        public int IndexOf(SLRProduction prod, int index = 0)
        {
            //return _productions.FindIndex(index, delegate(SLRProduction production) { return (production == prod); });
            return SLRProductions.IndexOf(prod, index);
        }

        public void RemoveAt(int index) { SLRProductions.RemoveAt(index); }

        public void Remove(CLRProduction production) { RemoveAt(IndexOf(production)); }

        public void RemoveRedundancy()
        {
            for (var i = Count - 1; i >= 0; --i)
            {
                var index = IndexOf(this[i], i + 1);
                if (-1 == index) continue;
                RemoveAt(index);
            }
        }

        public SLRProduction[] ToArray() { return SLRProductions.ToArray(); }

        public bool Equals(Closure closure) { return (this == closure); }

        public bool NotEquals(Closure closure) { return !Equals(closure); //(this != closure);
        }

        public abstract Closure GoToEntity(Entity X);

        #region ICloneable Members

        public abstract Object Clone();

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() { return SLRProductions.GetEnumerator(); }

        #endregion

        #region Overrided

        public override bool Equals(Object obj) { return (obj is Closure) ? Equals(obj as Closure) : base.Equals(obj); }

        public override int GetHashCode() { return ToString().GetHashCode() ^ base.GetHashCode(); }

        public override String ToString()
        {
            var sb = new StringBuilder(Title);
            foreach (SLRProduction production in this)
            {
                sb.AppendLine();
                sb.Append(production);
            }
            return sb.ToString();
        }

        #endregion

        #region Static

        public static bool operator ==(Closure closure1, Closure closure2)
        {
            if (ReferenceEquals(closure1, closure2)) return true;
            if (ReferenceEquals(null, closure1) || ReferenceEquals(null, closure2)) return false;

            if (closure1.Count != closure2.Count) return false;
            if (closure1.Grammar != closure2.Grammar) return false;

            for (var index = 0; index < closure1.Count; ++index)
                if (closure1[index] != closure2[index]) return false;
            return true;
        }

        public static bool operator !=(Closure closure1, Closure closure2) { return !(closure1 == closure2); }

        #endregion
    }

    public class SLRClosure : Closure
    {
        public SLRClosure(String title, Grammar grammar, IEnumerable<SLRProduction> productLR)
            : base(title, grammar)
        {
            if (default(SLRProduction[]) == productLR) return;

            SLRProductions = new List<SLRProduction>(productLR);
            var lastB = default(NonTerminal);
            //A --> α.Bβ, a
            for (var index = 0; index < Count; ++index)
            {
                var product = this[index];
                if (product.DotPosition == product.Count) continue;

                var entity = product.Product[product.DotPosition];

                if (!(entity is NonTerminal)) continue;

                var B = entity as NonTerminal; //B Non-Terminal
                if (B == lastB) continue;

                //B --> .rX, b
                foreach (var production in grammar.Productions)
                    if (production.Producer == B) 
                        Add(new SLRProduction(B, production.Product)); // gamma
                lastB = B;
            }
            RemoveRedundancy();
        }

        public SLRClosure(String title, Grammar grammar)
            : base(title, grammar) { }

        public SLRClosure()
            : this(default(String), new Grammar(), new[] {new SLRProduction()}) { }

        public SLRClosure(String title, Grammar grammar, SLRClosure closure)
            : base(title, grammar)
        {
            SLRProductions = (default(SLRClosure) != closure)
                                 ? closure.SLRProductions
                                 : default(List<SLRProduction>);
        }

        public override Closure GoToEntity(Entity X)
        {
            var closure = new SLRClosure("gotoX", Grammar);
            for (var index = 0; index < Count; ++index)
            {
                var product = this[index];
                var dot = product.DotPosition;
                if (dot == product.Count) continue;

                //Entity Y = product.Product[dot];
                if (X == product.Product[dot]) closure.Add(new SLRProduction(product.Producer, product.Product, dot + 1));
            }
            return closure;
        }

        public static explicit operator SLRProduction[](SLRClosure closure) { return closure.ToArray(); }

        public override Object Clone() { return new SLRClosure(Title, Grammar, new List<SLRProduction>(SLRProductions)); }
    }

    public class CLRClosure : SLRClosure
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
                if (default(Production) == product || product.DotPosition == product.Count) continue;

                var entity = product.Product[product.DotPosition];
                if (!(entity is NonTerminal)) continue;

                var B = entity as NonTerminal; //B Non-Terminal
                if (B == lastB) continue;

                var beta = product.Product.GetRange(product.DotPosition + 1);
                var a = product.LookAheads;
                //B --> .rX, b
                foreach (var production in grammar.Productions)
                {
                    if (production.Producer != B) continue;

                    //b = First(βa);
                    var b = default(EntityCollection<Terminal>);
                    foreach (var term in a) b += grammar.First(beta + term);

                    b = (default(EntityCollection<Terminal>) != b)
                            ? b.RemoveRedundancy() as EntityCollection<Terminal>
                            : new EntityCollection<Terminal>(new[] {(Terminal) "#"});

                    //Adding B --> .rX, b
                    Add(new CLRProduction(B, production.Product, b)); // gamma
                }
                lastB = B;
            }
            RemoveRedundancy();
        }

        public CLRClosure(String title, Grammar grammar)
            : base(title, grammar) { }

        public CLRClosure()
            : this(default(String), new Grammar(), new SLRProduction[] {new CLRProduction()}) { }

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
                if (default(Production) == prod1) continue;
                if (default(Production) == prod2) continue;

                prod1.LookAheads.Entities.AddRange(prod2.LookAheads);
                prod1 = prod1.LookAheads.RemoveRedundancy() as CLRProduction;
            }
        }

        public override Closure GoToEntity(Entity X)
        {
            var closure = new CLRClosure("gotoX", Grammar);
            for (var index = 0; index < Count; ++index)
            {
                var prodLALR = this[index] as CLRProduction;
                if (default(Production) == prodLALR) continue;

                var dot = prodLALR.DotPosition;
                if (dot == prodLALR.Count) continue;

                var Y = prodLALR.Product[dot];
                if (Y == X) closure.Add(new CLRProduction(prodLALR.Producer, prodLALR.Product, dot + 1, prodLALR.LookAheads));
            }
            return closure;
        }

        public static explicit operator CLRProduction[](CLRClosure closure) { return (CLRProduction[]) closure.ToArray(); }

        public override Object Clone() { return new SLRClosure(Title, Grammar, new List<SLRProduction>(SLRProductions)); }
    }
}