import React, { Component } from 'react';
import './Television.css';
import { withNavigate } from './withNavigate';
import CartIndicator from '../CartIndicator/CartIndicator';
import { CartContext } from '../CartContext/CartContext';

const TV_CACHE = {
    data: null,
    timestamp: null,
    expiry: 5 * 60 * 1000
};

const STORAGE_KEY = 'tvs_cache';
const ETAG_KEY = 'tvs_etag';

class Television extends Component {
    static contextType = CartContext;
    constructor(props) {
        super(props);
        this.state = {
            products: [],
            loading: true,
            error: null,
            fromCache: false,
            notification: { show: false, message: '', productName: '' }
        };

        this.abortController = null;
        this.notificationTimeout = null;
        this.loadFromCache();
    }

    loadFromCache() {
        try {
            if (TV_CACHE.data && TV_CACHE.timestamp &&
                (Date.now() - TV_CACHE.timestamp < TV_CACHE.expiry)) {
                this.setState({
                    products: TV_CACHE.data,
                    loading: false,
                    fromCache: true
                });
                return true;
            }

            const cached = localStorage.getItem(STORAGE_KEY);
            if (cached) {
                const { data, timestamp } = JSON.parse(cached);
                if (Date.now() - timestamp < TV_CACHE.expiry) {
                    TV_CACHE.data = data;
                    TV_CACHE.timestamp = timestamp;
                    this.setState({
                        products: data,
                        loading: false,
                        fromCache: true
                    });
                    return true;
                }
            }
        } catch (error) {
            console.warn('Ошибка загрузки кэша телевизоров:', error);
        }
        return false;
    }

    saveToCache(data) {
        try {
            TV_CACHE.data = data;
            TV_CACHE.timestamp = Date.now();
            localStorage.setItem(STORAGE_KEY, JSON.stringify({
                data: data.slice(0, 5),
                timestamp: Date.now()
            }));
            console.log('Телевизоры: данные сохранены в кэш');
        } catch (error) {
            console.warn('Ошибка сохранения кэша телевизоров:', error);
        }
    }

    componentDidMount() {
        console.log('Television component mounted');
        if (!this.state.fromCache) {
            this.fetchTVProducts();
        }
    }

    componentWillUnmount() {
        if (this.abortController) this.abortController.abort();
        if (this.notificationTimeout) clearTimeout(this.notificationTimeout);
    }

    fetchTVProducts = async () => {
        if (this.abortController) this.abortController.abort();
        this.abortController = new AbortController();

        try {
            console.log('Загрузка телевизоров');
            this.setState({ loading: true });

            const response = await fetch('http://localhost:5214/api/tvs', {
                signal: this.abortController.signal,
                headers: {
                    'If-None-Match': localStorage.getItem(ETAG_KEY) || '',
                    'Cache-Control': 'no-cache'
                }
            });

            console.log('Ответ от сервера:', response.status);

            if (response.status === 304) {
                console.log('Данные не изменились');
                this.setState({ loading: false });
                return;
            }

            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const products = await response.json();
            console.log('Получены данные:', products);
            console.log('Количество телевизоров:', products.length);

            const etag = response.headers.get('ETag');
            if (etag) localStorage.setItem(ETAG_KEY, etag);

            this.saveToCache(products);
            this.setState({ products, loading: false, error: null });

        } catch (error) {
            if (error.name === 'AbortError') {
                console.log('Запрос отменен');
                return;
            }
            console.error('Ошибка загрузки телевизоров:', error);

            if (TV_CACHE.data) {
                console.log('Используем кэшированные данные');
                this.setState({
                    products: TV_CACHE.data,
                    loading: false,
                    error: 'Используются кэшированные данные'
                });
            } else {
                this.setState({ error: error.message, loading: false });
            }
        }
    };

    showNotification = (productName) => {
        if (this.notificationTimeout) clearTimeout(this.notificationTimeout);
        
        this.setState({
            notification: { show: true, message: 'Товар добавлен в корзину!', productName: productName }
        });
        
        this.notificationTimeout = setTimeout(() => {
            this.setState({ notification: { show: false, message: '', productName: '' } });
        }, 3000);
    };

    handleProductClick = (productId) => {
        console.log(`Переход на товар с ID: ${productId}`);
        this.props.navigate(`/product/${productId}`);
    }

    getTVType = (tvName) => {
        if (!tvName) return 'standard';
        const n = tvName.toLowerCase();
        if (n.includes('samsung')) return 'samsung';
        if (n.includes('lg')) return 'lg';
        if (n.includes('sony')) return 'sony';
        if (n.includes('xiaomi')) return 'xiaomi';
        if (n.includes('oled')) return 'oled';
        if (n.includes('qled')) return 'qled';
        return 'standard';
    };

    getBrandName = (product) => {
        if (product.brandName) {
            return product.brandName;
        }
        if (product.brand && product.brand.name) {
            return product.brand.name;
        }

        const name = product.name?.toLowerCase() || '';
        if (name.includes('samsung')) return 'Samsung';
        if (name.includes('lg')) return 'LG';
        if (name.includes('sony')) return 'Sony';
        if (name.includes('xiaomi')) return 'Xiaomi';

        return 'Не указан';
    };

    getProductImage = async (productId) => {
        try {
            const response = await fetch(`http://localhost:5214/api/ProductImages/product/${productId}/main`);
            if (response.ok) {
                const image = await response.json();
                return image?.imageUrl || null;
            }
            return null;
        } catch (error) {
            console.error('Ошибка загрузки изображения:', error);
            return null;
        }
    };

    handleAddToCart = async (productId, quantity = 1, event, productName) => {
        if (event) {
            event.stopPropagation();
        }

        const result = await this.context.addToCart(productId, quantity);
        
        if (result.success) {
            this.showNotification(productName || 'Товар');
        } else {
            alert('Ошибка при добавлении в корзину');
        }
    };

    handleProfile = () => {
        this.props.navigate ? this.props.navigate('/Profile') : window.location.href = '/Profile';
    };

    handleClearCache = () => {
        localStorage.removeItem(STORAGE_KEY);
        localStorage.removeItem(ETAG_KEY);
        TV_CACHE.data = null;
        TV_CACHE.timestamp = null;
        this.setState({ products: [], loading: true });
        this.fetchTVProducts();
        alert('Кэш телевизоров очищен');
    };

    handleRetry = () => {
        this.setState({ loading: true, error: null });
        this.fetchTVProducts();
    };

    handleCartClick = () => {
        this.props.navigate('/cart');
    };

    handleGoBack = () => {
        this.props.navigate(-1);
    }

    handleSearch = (e) => {
        e.preventDefault();

        const input = e.target.querySelector('input[type="search"]')
        const query = input?.value;
        if (query && query.trim()) {
            this.props.navigate(`/search?q=${encodeURIComponent(query.trim())}`);
        }
    }

    render() {
        const { products, loading, error, notification } = this.state;
        const displayProducts = products.slice(0, 5);

        console.log('Рендер Television, состояние:', {
            productsCount: products.length,
            loading,
            error
        });

        return (
            <div className="television-page">
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

                <header className="header" id="header">
                    <nav className="nav container">
                        <a href="/" className="nav__logo">
                            <div className="logo-text">
                                <span className="logo-primary">BLANCE</span>
                                <span className="logo-secondary">vex</span>
                            </div>
                        </a>

                        <div className="search-full-width">
                            <div className="header-search-block expanded">
                                <form id="search-form" onSubmit={this.handleSearch}>
                                    <input
                                        id="search-input"
                                        name="q"
                                        type="search"
                                        placeholder="Поиск телевизоров..."
                                        className="header-search-block__search expanded"
                                    />
                                    <button type="submit" className="header-search-submit expanded"></button>
                                </form>
                            </div>
                        </div>

                        <div className="header-icons">
                            <button className="icon-btn user-icon" onClick={this.handleProfile}>
                                <svg width="20" height="20" viewBox="0 0 24 24" fill="black">
                                    <path d="M12 12C14.7614 12 17 9.76142 17 7C17 4.23858 14.7614 2 12 2C9.23858 2 7 4.23858 7 7C7 9.76142 9.23858 12 12 12Z" stroke="currentColor" strokeWidth="2"/>
                                    <path d="M20 22C20 17.5817 16.4183 14 12 14C7.58172 14 4 17.5817 4 22" stroke="currentColor" strokeWidth="2"/>
                                </svg>
                            </button>
                            <CartIndicator navigate={this.props.navigate} />
                        </div>
                    </nav>
                </header>

                <main className="main-content">
                    <div className="container television-container">
                        <button className="back-button" onClick={this.handleGoBack}>
                            Назад 
                        </button>
                        <h1 className="page-title">Телевизоры</h1>

                        <div className="products-grid-container">
                            {loading && (
                                <div className="loading-spinner">
                                    <div className="spinner"></div>
                                    <p>Загрузка телевизоров...</p>
                                </div>
                            )}

                            {error && !loading && (
                                <div className="error-message">
                                    <p>{error}</p>
                                    <button onClick={this.handleRetry} className="retry-btn">
                                        Попробовать снова
                                    </button>
                                    <button
                                        onClick={this.handleClearCache}
                                        className="retry-btn"
                                        style={{ marginLeft: '10px', backgroundColor: '#6c757d' }}
                                    >
                                        Очистить кэш
                                    </button>
                                </div>
                            )}

                            {!loading && !error && (
                                <div className="products-grid">
                                    {displayProducts.length > 0 ? (
                                        displayProducts.map((product, index) => {
                                            const tvType = this.getTVType(product.name);
                                            const tvClass = `product-card ${tvType}`;
                                            const brandName = this.getBrandName(product);
                                            const productName = product.name || `Телевизор ${index + 1}`;

                                            let imageUrl = product.imageUrl || product.ImageUrl || null;
                                            if (imageUrl && !imageUrl.startsWith('http')) {
                                                imageUrl = `http://localhost:5214${imageUrl.startsWith('/') ? '' : '/'}${imageUrl}`;
                                            }

                                            return (
                                                <div key={product.id || index} className={tvClass} onClick={() => this.handleProductClick(product.id)} style={{ cursor: 'pointer' }}>
                                                    <span className="brand-badge">{brandName}</span>

                                                    <div className="product-image-container">
                                                        {imageUrl ? (
                                                            <img
                                                                src={imageUrl}
                                                                alt={product.name || `Телевизор ${index + 1}`}
                                                                className={`product-image ${tvType}`}
                                                                loading="lazy"
                                                                onError={(e) => {
                                                                    e.target.onerror = null;
                                                                    e.target.style.display = 'none';
                                                                }}
                                                                onLoad={(e) => {
                                                                    e.target.style.opacity = '1';
                                                                }}
                                                                style={{
                                                                    opacity: 0,
                                                                    transition: 'opacity 0.3s ease'
                                                                }}
                                                            />
                                                        ) : (
                                                            <div className="no-image-placeholder">📷</div>
                                                        )}
                                                    </div>

                                                    <div className="product-content">
                                                        <h3 className="product-name">
                                                            {productName}
                                                        </h3>

                                                        <div className="product-specs">
                                                            <span className="spec">
                                                                <span className="spec-label">Бренд:</span>
                                                                <span className="spec-value">{brandName}</span>
                                                            </span>

                                                            {product.diagonal && (
                                                                <span className="spec">
                                                                    <span className="spec-label">Диагональ:</span>
                                                                    <span className="spec-value">{product.diagonal}</span>
                                                                </span>
                                                            )}

                                                            {product.resolution && (
                                                                <span className="spec">
                                                                    <span className="spec-label">Разрешение:</span>
                                                                    <span className="spec-value">{product.resolution}</span>
                                                                </span>
                                                            )}

                                                            {product.smartTV && (
                                                                <span className="spec">
                                                                    <span className="spec-label">Smart TV:</span>
                                                                    <span className="spec-value">{product.smartTV}</span>
                                                                </span>
                                                            )}

                                                            {product.description && (
                                                                <span className="spec">
                                                                    <span className="spec-label">Описание:</span>
                                                                    <span className="spec-value">
                                                                        {product.description.length > 50
                                                                            ? `${product.description.substring(0, 50)}...`
                                                                            : product.description}
                                                                    </span>
                                                                </span>
                                                            )}
                                                        </div>

                                                        <div className="product-price">
                                                            {product.price
                                                                ? `${product.price.toLocaleString('ru-RU')} ₽`
                                                                : "Цена не указана"}
                                                        </div>

                                                        <div className="product-availability">
                                                            {product.isActive !== false ? (
                                                                <span className="in-stock">В наличии</span>
                                                            ) : (
                                                                <span className="out-of-stock">Нет в наличии</span>
                                                            )}
                                                        </div>

                                                        <button
                                                            className="add-to-cart-btn"
                                                            onClick={(e) => this.handleAddToCart(product.id, 1, e, productName)}
                                                            disabled={product.isActive === false}
                                                        >
                                                            {product.isActive !== false ? "В корзину" : "Нет в наличии"}
                                                        </button>
                                                    </div>
                                                </div>
                                            );
                                        })
                                    ) : (
                                        <div className="no-products">
                                            <p>В базе данных нет телевизоров</p>
                                            <div className="suggestions">
                                                <p>Что можно сделать:</p>
                                                <ul>
                                                    <li>Добавьте телевизоры через админ-панель</li>
                                                    <li>Убедитесь, что сервер запущен</li>
                                                    <li>Проверьте подключение к базе данных</li>
                                                </ul>
                                            </div>
                                            <button onClick={this.handleRetry} className="clear-filters-btn">
                                                Обновить список
                                            </button>
                                            <button
                                                onClick={this.handleClearCache}
                                                className="clear-filters-btn"
                                                style={{ marginLeft: '10px', backgroundColor: '#dc3545' }}
                                            >
                                                Очистить кэш
                                            </button>
                                        </div>
                                    )}
                                </div>
                            )}
                        </div>
                    </div>
                </main>
            </div>
        );
    }
}

export default withNavigate(Television);