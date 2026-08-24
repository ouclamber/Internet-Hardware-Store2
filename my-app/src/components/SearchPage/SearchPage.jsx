import React, { Component } from 'react';
import './SearchPage.css';
import { withNavigate } from './withNavigate';
import CartIndicator from '../CartIndicator/CartIndicator';
import { CartContext } from '../CartContext/CartContext';

class SearchPage extends Component {
    static contextType = CartContext;
    constructor(props) {
        super(props);
        this.state = {
            query: '',
            products: [],
            loading: false,
            error: null,
            searchPerformed: false,
            categories: [],
            selectedCategory: 'all',
            minPrice: '',
            maxPrice: '',
            sortBy: 'relevance',
            notification: { show: false, message: '', productName: '' }
        };
        this.searchTimeout = null;
        this.abortController = null;
        this.searchCache = new Map();
        this.categoriesCache = null;
        this.categoriesCacheTime = null;
        this.notificationTimeout = null;
    }

    componentDidMount() {
        const urlParams = new URLSearchParams(window.location.search);
        const query = urlParams.get('q') || '';
        const category = urlParams.get('category') || 'all';
        
        if (query) {
            this.setState({ query, selectedCategory: category }, () => {
                this.performSearch();
            });
        } else {
            this.loadCategories();
        }
    }

    componentWillUnmount() {
        if (this.searchTimeout) {
            clearTimeout(this.searchTimeout);
        }
        if (this.abortController) {
            this.abortController.abort();
        }
        if (this.notificationTimeout) {
            clearTimeout(this.notificationTimeout);
        }
    }

    showNotification = (productName) => {
        if (this.notificationTimeout) clearTimeout(this.notificationTimeout);
        
        this.setState({
            notification: { show: true, message: 'Товар добавлен в корзину!', productName: productName }
        });
        
        this.notificationTimeout = setTimeout(() => {
            this.setState({ notification: { show: false, message: '', productName: '' } });
        }, 3000);
    };

    getCachedCategories = () => {
        if (this.categoriesCache && this.categoriesCacheTime && 
            (Date.now() - this.categoriesCacheTime) < 300000) { // 5 минут
            console.log('Загрузка категорий из кэша');
            return this.categoriesCache;
        }
        return null;
    };

    setCachedCategories = (categories) => {
        this.categoriesCache = categories;
        this.categoriesCacheTime = Date.now();
        console.log('Категории сохранены в кэш');
    };

    getCachedSearchResults = (cacheKey) => {
        const cached = this.searchCache.get(cacheKey);
        if (cached && (Date.now() - cached.timestamp) < 300000) { // 5 минут
            console.log(`Загрузка результатов поиска из кэша: ${cacheKey}`);
            return cached.data;
        }
        return null;
    };

    setCachedSearchResults = (cacheKey, data) => {
        this.searchCache.set(cacheKey, {
            data: data,
            timestamp: Date.now()
        });
        console.log(`Результаты поиска сохранены в кэш: ${cacheKey}`);

        if (this.searchCache.size > 20) {
            const oldestKey = this.searchCache.keys().next().value;
            this.searchCache.delete(oldestKey);
            console.log(`Удален старый кэш: ${oldestKey}`);
        }
    };

    getLocalStorageSearch = (cacheKey) => {
        try {
            const cachedData = localStorage.getItem(cacheKey);
            const cacheTime = localStorage.getItem(`${cacheKey}_time`);
            
            if (cachedData && cacheTime && (Date.now() - parseInt(cacheTime)) < 300000) {
                console.log(`Загрузка результатов поиска из localStorage: ${cacheKey}`);
                return JSON.parse(cachedData);
            }
        } catch (error) {
            console.error('Ошибка чтения из localStorage:', error);
        }
        return null;
    };

    setLocalStorageSearch = (cacheKey, data) => {
        try {
            localStorage.setItem(cacheKey, JSON.stringify(data));
            localStorage.setItem(`${cacheKey}_time`, Date.now().toString());
            console.log(`Результаты поиска сохранены в localStorage: ${cacheKey}`);
        } catch (error) {
            console.error('Ошибка сохранения в localStorage:', error);
        }
    };

    loadCategories = async () => {
        try {
            let categories = this.getCachedCategories();

            if (!categories) {
                const cachedData = localStorage.getItem('categories_cache');
                const cacheTime = localStorage.getItem('categories_cache_time');
                
                if (cachedData && cacheTime && (Date.now() - parseInt(cacheTime)) < 300000) {
                    categories = JSON.parse(cachedData);
                    this.setCachedCategories(categories);
                    console.log('Категории загружены из localStorage');
                }
            }

            if (categories) {
                this.setState({ categories });
                return;
            }

            console.log('Загрузка категорий с сервера');
            const response = await fetch('http://localhost:5214/api/categories/with-products');
            if (response.ok) {
                categories = await response.json();
                this.setCachedCategories(categories);

                localStorage.setItem('categories_cache', JSON.stringify(categories));
                localStorage.setItem('categories_cache_time', Date.now().toString());
                
                this.setState({ categories });
            }
        } catch (error) {
            console.error('Ошибка загрузки категорий:', error);
        }
    };

    generateCacheKey = () => {
        const { query, selectedCategory, minPrice, maxPrice, sortBy } = this.state;
        return `search_${query}_cat${selectedCategory}_price${minPrice}_${maxPrice}_sort${sortBy}`;
    };

    handleInputChange = (e) => {
        const query = e.target.value;
        this.setState({ query });

        if (this.searchTimeout) {
            clearTimeout(this.searchTimeout);
        }
        
        this.searchTimeout = setTimeout(() => {
            if (query.length >= 2 || query.length === 0) {
                this.performSearch();
            }
        }, 500);
    };

    performSearch = async () => {
        const { query, selectedCategory, minPrice, maxPrice, sortBy } = this.state;
        
        if (!query.trim() && selectedCategory === 'all') {
            this.setState({ products: [], searchPerformed: false, loading: false });
            return;
        }
        
        if (this.abortController) {
            this.abortController.abort();
        }
        
        this.abortController = new AbortController();
        
        const cacheKey = this.generateCacheKey();

        let cachedProducts = this.getCachedSearchResults(cacheKey);

        if (!cachedProducts) {
            cachedProducts = this.getLocalStorageSearch(cacheKey);
            if (cachedProducts) {
                this.setCachedSearchResults(cacheKey, cachedProducts);
            }
        }

        if (cachedProducts) {
            console.log('Используем кэшированные результаты поиска');
            
            let sortedProducts = this.sortProducts([...cachedProducts], sortBy);
            
            this.setState({ 
                products: sortedProducts, 
                loading: false,
                searchPerformed: true,
                error: null
            });

            this.refreshSearchInBackground(cacheKey);
            return;
        }
        
        this.setState({ loading: true, error: null, searchPerformed: true });
        
        try {
            let url = `http://localhost:5214/api/products/search?q=${encodeURIComponent(query)}`;
            
            if (selectedCategory !== 'all') {
                url += `&categoryId=${selectedCategory}`;
            }
            if (minPrice) {
                url += `&minPrice=${minPrice}`;
            }
            if (maxPrice) {
                url += `&maxPrice=${maxPrice}`;
            }
            if (sortBy !== 'relevance') {
                url += `&sortBy=${sortBy}`;
            }
            
            console.log('Запрос к API:', url);
            
            const response = await fetch(url, {
                signal: this.abortController.signal
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            
            const products = await response.json();

            this.setCachedSearchResults(cacheKey, products);
            this.setLocalStorageSearch(cacheKey, products);

            let sortedProducts = this.sortProducts([...products], sortBy);
            
            this.setState({ 
                products: sortedProducts, 
                loading: false,
                error: null
            });

            const urlParams = new URLSearchParams();
            if (query) urlParams.set('q', query);
            if (selectedCategory !== 'all') urlParams.set('category', selectedCategory);
            window.history.pushState({}, '', `${window.location.pathname}?${urlParams.toString()}`);

            this.preloadPopularProducts();
            
        } catch (error) {
            if (error.name === 'AbortError') return;
            console.error('Ошибка поиска:', error);
            this.setState({ 
                error: 'Ошибка при выполнении поиска', 
                loading: false,
                products: []
            });
        }
    };

    refreshSearchInBackground = async (cacheKey) => {
        const { query, selectedCategory, minPrice, maxPrice, sortBy } = this.state;
        
        try {
            console.log('Фоновое обновление результатов поиска...');
            
            let url = `http://localhost:5214/api/products/search?q=${encodeURIComponent(query)}`;
            
            if (selectedCategory !== 'all') {
                url += `&categoryId=${selectedCategory}`;
            }
            if (minPrice) {
                url += `&minPrice=${minPrice}`;
            }
            if (maxPrice) {
                url += `&maxPrice=${maxPrice}`;
            }
            
            const response = await fetch(url, {
                signal: this.abortController?.signal
            });
            
            if (response.ok) {
                const freshProducts = await response.json();

                this.setCachedSearchResults(cacheKey, freshProducts);
                this.setLocalStorageSearch(cacheKey, freshProducts);

                const currentProducts = this.state.products;
                if (JSON.stringify(currentProducts) !== JSON.stringify(freshProducts)) {
                    console.log('Результаты поиска обновлены из фонового запроса');
                    let sortedProducts = this.sortProducts([...freshProducts], sortBy);
                    this.setState({ products: sortedProducts });
                }
            }
        } catch (error) {
            console.error('Ошибка фонового обновления:', error);
        }
    };

    preloadPopularProducts = async () => {
        try {
            const cacheKey = 'popular_products';
            const cachedData = localStorage.getItem(cacheKey);
            const cacheTime = localStorage.getItem(`${cacheKey}_time`);
            
            if (cachedData && cacheTime && (Date.now() - parseInt(cacheTime)) < 600000) { // 10 минут
                console.log('Популярные товары уже в кэше');
                return;
            }
            
            console.log('Предзагрузка популярных товаров...');
            const response = await fetch('http://localhost:5214/api/products/popular?limit=8');
            
            if (response.ok) {
                const popularProducts = await response.json();
                localStorage.setItem(cacheKey, JSON.stringify(popularProducts));
                localStorage.setItem(`${cacheKey}_time`, Date.now().toString());
                console.log('Популярные товары сохранены в кэш');
            }
        } catch (error) {
            console.error('Ошибка предзагрузки популярных товаров:', error);
        }
    };

    sortProducts = (products, sortBy) => {
        let sortedProducts = [...products];
        
        if (sortBy === 'price_asc') {
            sortedProducts.sort((a, b) => (a.price || 0) - (b.price || 0));
        } else if (sortBy === 'price_desc') {
            sortedProducts.sort((a, b) => (b.price || 0) - (a.price || 0));
        } else if (sortBy === 'name_asc') {
            sortedProducts.sort((a, b) => (a.name || '').localeCompare(b.name || ''));
        }
        
        return sortedProducts;
    };

    clearSearchCache = () => {
        this.searchCache.clear();
        console.log('Кэш поиска очищен');
    };

    clearAllCache = () => {
        this.clearSearchCache();
        this.categoriesCache = null;
        this.categoriesCacheTime = null;
        
        const keys = Object.keys(localStorage);
        keys.forEach(key => {
            if (key.startsWith('search_') || key === 'categories_cache' || key === 'popular_products') {
                localStorage.removeItem(key);
                localStorage.removeItem(`${key}_time`);
            }
        });
        console.log('Весь кэш поиска очищен');
    };

    handleCategoryChange = (categoryId) => {
        this.setState({ selectedCategory: categoryId }, () => {
            this.performSearch();
        });
    };

    handlePriceChange = (e) => {
        this.setState({ [e.target.name]: e.target.value }, () => {
            this.performSearch();
        });
    };

    handleSortChange = (e) => {
        const { products, sortBy } = this.state;
        const newSortBy = e.target.value;
        
        this.setState({ sortBy: newSortBy }, () => {
            const sortedProducts = this.sortProducts([...products], newSortBy);
            this.setState({ products: sortedProducts });

            const cacheKey = this.generateCacheKey();
            const cachedProducts = this.getCachedSearchResults(cacheKey);
            if (cachedProducts) {
                this.setCachedSearchResults(cacheKey, cachedProducts);
            }
        });
    };

    handleProductClick = (productId) => {
        this.props.navigate(`/product/${productId}`);
    };

    handleClearFilters = () => {
        this.setState({
            selectedCategory: 'all',
            minPrice: '',
            maxPrice: '',
            sortBy: 'relevance'
        }, () => {
            this.performSearch();
        });
    };

    handleProfile = () => {
        this.props.navigate('/Profile');
    };

    handleGoBack = () => {
        this.props.navigate('/HomePage');
    }

    formatPrice = (price) => {
        if (!price && price !== 0) return 'Цена не указана';
        return `${price.toLocaleString('ru-RU')} ₽`;
    };

    getProductImage = (product) => {
        let imageUrl = null;

        if (product.imageUrl) {
            imageUrl = product.imageUrl;
        } else if (product.mainImage) {
            imageUrl = product.mainImage;
        } else if (product.images && product.images.length > 0) {
            const img = product.images[0];
            imageUrl = img.imageUrl || img.ImageUrl || null;
        }

        if (imageUrl && !imageUrl.startsWith('http')) {
            imageUrl = `http://localhost:5214${imageUrl.startsWith('/') ? '' : '/'}${imageUrl}`;
        }
        
        return imageUrl;
    };

    render() {
        const { query, products, loading, error, searchPerformed, categories, selectedCategory, minPrice, maxPrice, sortBy, notification } = this.state;
        
        return (
            <div className="search-page">
                {notification.show && (
                    <div className="cart-notification">
                        <div className="cart-notification-content">
                            <div className="cart-notification-text">
                                <strong>{notification.productName}</strong>
                                <span>{notification.message}</span>
                            </div>
                        </div>
                    </div>
                )}

                <header className="header">
                    <nav className="nav container">
                        <a href="/" className="nav__logo">
                            <div className="logo-text">
                                <span className="logo-primary">GLANCE</span>
                                <span className="logo-secondary">vex</span>
                            </div>
                        </a>

                        <div className="search-full-width">
                            <div className="header-search-block">
                                <form onSubmit={(e) => { e.preventDefault(); this.performSearch(); }}>
                                    <input
                                        type="search"
                                        placeholder="Поиск товаров..."
                                        className="header-search-block__search"
                                        value={query}
                                        onChange={this.handleInputChange}
                                        autoFocus
                                    />
                                </form>
                            </div>
                        </div>

                        <div className="header-icons">
                            <button className="icon-btn user-icon" onClick={this.handleProfile}>
                                <svg width="20" height="20" viewBox="0 0 24 24" fill="black" stroke="white">
                                    <path d="M12 12C14.7614 12 17 9.76142 17 7C17 4.23858 14.7614 2 12 2C9.23858 2 7 4.23858 7 7C7 9.76142 9.23858 12 12 12Z" stroke="currentColor" strokeWidth="2"/>
                                    <path d="M20 22C20 17.5817 16.4183 14 12 14C7.58172 14 4 17.5817 4 22" stroke="currentColor" strokeWidth="2"/>
                                </svg>
                            </button>
                            <CartIndicator navigate={this.props.navigate} />
                        </div>
                    </nav>
                </header>

                <main className="main-content">
                    <div className="container search-container">
                        <div className="search-header">
                            <h1 className="search-title">
                                {searchPerformed ? (
                                    query ? `Результаты поиска: "${query}"` : 'Все товары'
                                ) : (
                                    'Поиск товаров'
                                )}
                            </h1>
                            {searchPerformed && (
                                <p className="search-results-count">
                                    Найдено {products.length} товаров
                                </p>
                            )}

                            <button className="back-button" onClick={this.handleGoBack}>
                                На главную 
                            </button>
                        </div>

                        <div className="search-layout">
                            <aside className="search-filters">
                                <div className="filter-section">
                                    <h3 className="filter-title">Категории</h3>
                                    <div className="filter-options">
                                        <label className="filter-option">
                                            <input
                                                type="radio"
                                                name="category"
                                                value="all"
                                                checked={selectedCategory === 'all'}
                                                onChange={() => this.handleCategoryChange('all')}
                                            />
                                            <span>Все категории</span>
                                        </label>
                                        {categories.map(category => (
                                            <label key={category.id} className="filter-option">
                                                <input
                                                    type="radio"
                                                    name="category"
                                                    value={category.id}
                                                    checked={selectedCategory === category.id}
                                                    onChange={() => this.handleCategoryChange(category.id)}
                                                />
                                                <span>{category.name}</span>
                                                <span className="filter-count">{category.productCount}</span>
                                            </label>
                                        ))}
                                    </div>
                                </div>

                                <div className="filter-section">
                                    <h3 className="filter-title">Цена</h3>
                                    <div className="price-range">
                                        <input
                                            type="number"
                                            name="minPrice"
                                            className="price-input"
                                            placeholder="От"
                                            value={minPrice}
                                            onChange={this.handlePriceChange}
                                        />
                                        <span className="price-separator">—</span>
                                        <input
                                            type="number"
                                            name="maxPrice"
                                            className="price-input"
                                            placeholder="До"
                                            value={maxPrice}
                                            onChange={this.handlePriceChange}
                                        />
                                    </div>
                                </div>

                                <div className="filter-section">
                                    <h3 className="filter-title">Сортировка</h3>
                                    <select className="sort-select" value={sortBy} onChange={this.handleSortChange}>
                                        <option value="relevance">По релевантности</option>
                                        <option value="price_asc">Сначала дешевые</option>
                                        <option value="price_desc">Сначала дорогие</option>
                                        <option value="name_asc">По названию (А-Я)</option>
                                    </select>
                                </div>

                                {(selectedCategory !== 'all' || minPrice || maxPrice || sortBy !== 'relevance') && (
                                    <button className="clear-filters-btn" onClick={this.handleClearFilters}>
                                        Сбросить все фильтры
                                    </button>
                                )}
                            </aside>

                            <div className="search-results">
                                {loading && (
                                    <div className="loading-spinner">
                                        <div className="spinner"></div>
                                        <p>Поиск...</p>
                                    </div>
                                )}

                                {error && (
                                    <div className="error-message">
                                        <p>{error}</p>
                                        <button onClick={this.performSearch} className="retry-btn">
                                            Повторить
                                        </button>
                                    </div>
                                )}

                                {!loading && !error && searchPerformed && products.length === 0 && (
                                    <div className="no-results">
                                        <h3>Ничего не найдено</h3>
                                        <p>Попробуйте изменить поисковый запрос или фильтры</p>
                                    </div>
                                )}

                                {!loading && !error && products.length > 0 && (
                                    <div className="products-grid">
                                        {products.map((product) => {
                                            const imageUrl = this.getProductImage(product);
                                            
                                            return (
                                                <div 
                                                    key={product.id} 
                                                    className="product-card"
                                                    onClick={() => this.handleProductClick(product.id)}
                                                >
                                                    <div className="product-image-container">
                                                        {imageUrl ? (
                                                            <img 
                                                                src={imageUrl} 
                                                                alt={product.name}
                                                                className="product-image"
                                                                loading="lazy"
                                                                onError={(e) => {
                                                                    console.error('Ошибка загрузки изображения:', imageUrl);
                                                                    e.target.onerror = null;
                                                                    e.target.style.display = 'none';
                                                                    const parent = e.target.parentElement;
                                                                    if (parent) {
                                                                        const placeholder = document.createElement('div');
                                                                        placeholder.className = 'no-image-placeholder';
                                                                        placeholder.textContent = '📷';
                                                                        parent.appendChild(placeholder);
                                                                    }
                                                                }}
                                                            />
                                                        ) : (
                                                            <div className="no-image-placeholder">📷</div>
                                                        )}
                                                    </div>
                                                    <div className="product-info">
                                                        <h3 className="product-name">{product.name}</h3>
                                                        {product.brandName && (
                                                            <div className="product-brand">{product.brandName}</div>
                                                        )}
                                                        {product.categoryName && (
                                                            <div className="product-category">{product.categoryName}</div>
                                                        )}
                                                        <div className="product-price">{this.formatPrice(product.price)}</div>
                                                        <div className="product-availability">
                                                            {product.isActive !== false ? (
                                                                <span className="in-stock">В наличии</span>
                                                            ) : (
                                                                <span className="out-of-stock">Нет в наличии</span>
                                                            )}
                                                        </div>
                                                        <button 
                                                            className="add-to-cart-btn"
                                                            onClick={(e) => {
                                                                e.stopPropagation();
                                                                this.context.addToCart(product.id, 1);
                                                                this.showNotification(product.name);
                                                            }}
                                                            disabled={product.isActive === false}
                                                        >
                                                            {product.isActive !== false ? "В корзину" : "Нет в наличии"}
                                                        </button>
                                                    </div>
                                                </div>
                                            );
                                        })}
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                </main>
            </div>
        );
    }
}

export default withNavigate(SearchPage);