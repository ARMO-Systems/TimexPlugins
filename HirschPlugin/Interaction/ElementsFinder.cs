using System;
using System.Collections.Generic;
using System.Linq;

namespace Timex.HirschPlugin.Interaction
{
    internal sealed class ElementsFinder< T > where T : class
    {
        #region Constants and Fields

        private readonly Dictionary< string, T > dicElementId = new Dictionary< string, T >();
        private readonly Func< T, string > funcGetId;

        #endregion

        public bool IsEmpty
        {
            get { return dicElementId.Count == 0; }
        }

        #region Constructors and Destructors

        public ElementsFinder( Func< T, string > func )
        {
            funcGetId = func;
        }

        #endregion

        #region Public Methods and Operators

        public void AddIfNotExists( T elem )
        {
            var id = funcGetId( elem );
            if ( id != null && !dicElementId.ContainsKey( id ) )
                dicElementId.Add( id, elem );
        }

        public T Find( string elementId )
        {
            return Contains( elementId ) ? dicElementId[ elementId ] : null;
        }

        private bool Contains( string elementId )
        {
            return dicElementId.ContainsKey( elementId );
        }

        public void Reload( IEnumerable< T > elem )
        {
            dicElementId.Clear();
            elem.ToList().ForEach( AddIfNotExists );
        }

        #endregion
    }
}