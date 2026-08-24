import React, { Component } from 'react';
import './HomePage.css';
import { withNavigate } from './withNavigate'; 
import CartIndicator from '../CartIndicator/CartIndicator';

// КЭШ
const CATEGORY_CACHE = {
    data: null,
    timestamp: null,
    expiry: 5 * 60 * 1000 
};
const STORAGE_KEY = 'categories_cache';
const ETAG_KEY = 'categories_etag';

class HomePage extends Component {
    constructor(props) {
        super(props);
        this.state = {
            categories: [],
            categoriesLoading: true,
            error: null,
            fromCache: false
        };
        
        this.abortController = null;
        this.loadFromCache();
    }

    loadFromCache() {
        try {
            if (CATEGORY_CACHE.data && CATEGORY_CACHE.timestamp && 
                (Date.now() - CATEGORY_CACHE.timestamp < CATEGORY_CACHE.expiry)) {
                console.log('Загружено из кэша памяти');
                this.setState({ 
                    categories: CATEGORY_CACHE.data, 
                    categoriesLoading: false, 
                    fromCache: true 
                });
                return true;
            }
            
            const cached = localStorage.getItem(STORAGE_KEY);
            if (cached) {
                const { data, timestamp } = JSON.parse(cached);
                if (Date.now() - timestamp < CATEGORY_CACHE.expiry) {
                    console.log('Загружено из localStorage');
                    CATEGORY_CACHE.data = data;
                    CATEGORY_CACHE.timestamp = timestamp;
                    this.setState({ 
                        categories: data, 
                        categoriesLoading: false, 
                        fromCache: true 
                    });
                    return true;
                }
            }
        } catch (error) {
            console.warn('Ошибка загрузки кэша:', error);
        }
        return false;
    }

    saveToCache(data) {
        try {
            CATEGORY_CACHE.data = data;
            CATEGORY_CACHE.timestamp = Date.now();
            localStorage.setItem(STORAGE_KEY, JSON.stringify({
                data: data,
                timestamp: Date.now()
            }));
            console.log('Данные сохранены в кэш');
        } catch (error) {
            console.warn('Ошибка сохранения кэша:', error);
        }
    }

    componentDidMount() {
        if (!this.state.fromCache) {
            this.fetchCategoriesFromAPI();
        }

        this.intervalId = setInterval(() => {
            this.cleanCache();
        }, 60 * 1000);
    }

    componentWillUnmount() {
        if (this.abortController) {
            this.abortController.abort();
        }
        if (this.intervalId) {
            clearInterval(this.intervalId);
        }
    }

    cleanCache() {
        try {
            const cached = localStorage.getItem(STORAGE_KEY);
            if (cached) {
                const { timestamp } = JSON.parse(cached);
                if (Date.now() - timestamp > CATEGORY_CACHE.expiry * 2) {
                    localStorage.removeItem(STORAGE_KEY);
                    console.log('Устаревший кэш удален');
                }
            }
        } catch (error) {
            console.warn('Ошибка очистки кэша:', error);
        }
    }

    fetchCategoriesFromAPI = async () => {
        if (this.abortController) {
            this.abortController.abort();
        }
        
        this.abortController = new AbortController();

        try {
            this.setState({ categoriesLoading: true });
            
            const response = await fetch('http://localhost:5214/api/categories', {
                signal: this.abortController.signal,
                headers: { 
                    'If-None-Match': localStorage.getItem(ETAG_KEY) || '',
                    'Cache-Control': 'no-cache'
                }
            });

            if (response.status === 304) {
                console.log('Данные не изменились');
                this.setState({ categoriesLoading: false });
                return;
            }

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();
            console.log('Получены категории:', data);

            const categoryOrder = ['Телевизоры', 'Ноутбуки', 'Компьютеры', 'Смартфоны', 'Колонки'];

            const formattedCategories = categoryOrder
                .map((name) => {
                    const category = data.find(c => c.Name === name);
                    if (category) {
                        return {
                            id: category.Id,
                            name: category.Name,
                            description: category.Description || `${category.Name} в нашем магазине`,
                            productCount: category.ProductsCount || 0,
                            imageUrl: category.ImageUrl || null
                        };
                    }
                    return null;
                })
                .filter(c => c !== null);
            
            console.log('Отформатированные категории:', formattedCategories);
            
            const etag = response.headers.get('ETag');
            if (etag) {
                localStorage.setItem(ETAG_KEY, etag);
            }
            
            this.saveToCache(formattedCategories);
            this.setState({ 
                categories: formattedCategories,
                categoriesLoading: false,
                error: null
            });

        } catch (error) {
            if (error.name === 'AbortError') {
                console.log('Запрос отменен');
                return;
            }
            
            console.error('Ошибка загрузки категорий:', error);
            
            if (CATEGORY_CACHE.data && CATEGORY_CACHE.data.length > 0) {
                console.log('Используем кэш после ошибки');
                this.setState({ 
                    categories: CATEGORY_CACHE.data,
                    categoriesLoading: false,
                    error: 'Используются кэшированные данные'
                });
            } else {
                this.setState({ 
                    error: `Не удалось загрузить категории: ${error.message}`,
                    categoriesLoading: false 
                });
            }
        }
    };

    getCategoryImage = (category) => {
        let imageUrl = category.imageUrl || null;
        
        if (imageUrl && !imageUrl.startsWith('http')) {
            imageUrl = `http://localhost:5214${imageUrl.startsWith('/') ? '' : '/'}${imageUrl}`;
        }
        
        console.log('Изображение категории:', category.name, 'URL:', imageUrl);
        return imageUrl;
    };

    handleCategoryClick = (categoryId, categoryName) => {
        console.log(`Клик по категории: ${categoryName} (ID: ${categoryId})`);
        
        const name = categoryName.toLowerCase();
        
        if (name.includes('телевизор')) {
            this.props.navigate ? this.props.navigate('/Television') : window.location.href = '/Television';
        } else if (name.includes('ноутбук')) {
            this.props.navigate ? this.props.navigate('/Labtop') : window.location.href = '/Labtop';
        } else if (name.includes('компьютер')) {
            this.props.navigate ? this.props.navigate('/Computer') : window.location.href = '/Computer';
        } else if (name.includes('колонки')) {
            this.props.navigate ? this.props.navigate('/Speaker') : window.location.href = '/Speaker';
        } else if (name.includes('смартфоны')) {
            this.props.navigate ? this.props.navigate('/Phone') : window.location.href = '/Phone';
        }
        else {
            alert(`Категория "${categoryName}" пока в разработке`);
        }
    };

    handleProfile = () => {
        this.props.navigate ? this.props.navigate('/Profile') : window.location.href = '/Profile';
    };

    handleClearCache = () => {
        localStorage.removeItem(STORAGE_KEY);
        localStorage.removeItem(ETAG_KEY);
        CATEGORY_CACHE.data = null;
        CATEGORY_CACHE.timestamp = null;
        this.setState({ categories: [], categoriesLoading: true });
        this.fetchCategoriesFromAPI();
        alert('Кэш очищен');
    };

    handleSearch = (e) => {
        e.preventDefault();

        const input = e.target.querySelector('input[type="search"]')
        const query = input?.value;
        if (query && query.trim()) {
            this.props.navigate(`/search?q=${encodeURIComponent(query.trim())}`);
        }
    }

    render() {
        const { categories, categoriesLoading, error } = this.state;

        return (
            <div className="home-page">
                <header className="header">
                    <nav className="nav container">
                        <a href="#" className="nav__logo" onClick={(e) => { 
                            e.preventDefault(); 
                            this.props.navigate ? this.props.navigate('/') : window.location.href = '/'; 
                        }}>
                            <div className="logo-text">
                                <span className="logo-primary">BLANCE</span>
                                <span className="logo-secondary">vex</span>
                            </div>
                        </a>

                        <div className="search-full-width">
                            <div className="header-search-block">
                                <form onSubmit={this.handleSearch}>
                                    <input 
                                        type="search" 
                                        placeholder="Поиск товаров, услуг или информации..." 
                                        className="header-search-block__search" 
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
                    <div className="container">
                        <div className="catalog-header">
                            <h1 className="catalog-title">Каталог</h1>
                            <p className="catalog-subtitle">Выберите интересующую вас категорию товаров</p>
                        </div>

                        {categoriesLoading && (
                            <div className="loading-spinner">
                                <div className="spinner"></div>
                                <p>Загрузка категорий...</p>
                            </div>
                        )}

                        {error && !categoriesLoading && (
                            <div className="error-message">
                                <p>{error}</p>
                                <button onClick={this.fetchCategoriesFromAPI} className="retry-btn">
                                    Повторить
                                </button>
                                <button onClick={this.handleClearCache} className="retry-btn" style={{marginLeft: '10px'}}>
                                    Очистить кэш
                                </button>
                            </div>
                        )}

                        {!categoriesLoading && !error && (
                            <div className="categories-grid">
                                {categories.length > 0 ? (
                                    categories.map((category, index) => {
                                        const colorClass = `category-color-${(index % 5) + 1}`;
                                        const isEmpty = category.productCount === 0;
                                        const imageUrl = this.getCategoryImage(category);
                                        
                                        return (
                                            <div 
                                                key={category.id} 
                                                className={`category-card ${colorClass} ${isEmpty ? 'empty' : ''}`}
                                                onClick={() => this.handleCategoryClick(category.id, category.name)}
                                            >
                                                <div className="category-image-container">
                                                    {imageUrl ? (
                                                        <img 
                                                            src={imageUrl} 
                                                            alt={category.name}
                                                            className="category-image"
                                                            loading="lazy"
                                                            onError={(e) => {
                                                                console.error('Ошибка загрузки изображения категории:', imageUrl);
                                                                e.target.onerror = null;
                                                                e.target.style.display = 'none';
                                                                const parent = e.target.parentElement;
                                                                if (parent) {
                                                                    const noImageDiv = document.createElement('div');
                                                                    noImageDiv.className = 'category-no-image';
                                                                    noImageDiv.textContent = '📷';
                                                                    noImageDiv.style.cssText = 'display: flex; align-items: center; justify-content: center; width: 100%; height: 100%; font-size: 48px; background: #f5f5f5;';
                                                                    parent.appendChild(noImageDiv);
                                                                }
                                                            }}
                                                        />
                                                    ) : (
                                                        <div className="category-no-image">📷</div>
                                                    )}
                                                    <div className="category-overlay"></div>
                                                </div>
                                                <div className="category-content">
                                                    <div className="category-header">
                                                        <h3 className="category-name">{category.name}</h3>
                                                        <span className="category-badge">
                                                            {category.productCount} товар
                                                            {category.productCount !== 1 ? 'ов' : ''}
                                                        </span>
                                                    </div>
                                                    {category.description && (
                                                        <p className="category-description">
                                                            {category.description.length > 80 
                                                                ? `${category.description.substring(0, 80)}...` 
                                                                : category.description}
                                                        </p>
                                                    )}
                                                    <div className="category-footer">
                                                        <span className="category-action">
                                                            Смотреть товары
                                                            <svg className="arrow-icon" width="16" height="16" viewBox="0 0 24 24" fill="none">
                                                                <path d="M5 12H19" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                                                                <path d="M12 5L19 12L12 19" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                                                            </svg>
                                                        </span>
                                                    </div>
                                                </div>
                                            </div>
                                        );
                                    })
                                ) : (
                                    <div className="no-categories">
                                        <p>Категории не найдены</p>
                                        <button onClick={this.fetchCategoriesFromAPI} className="retry-btn">
                                            Обновить
                                        </button>
                                    </div>
                                )}
                            </div>
                        )}
                    </div>
                </main>
            </div>
        );
    }
}

export default withNavigate(HomePage);