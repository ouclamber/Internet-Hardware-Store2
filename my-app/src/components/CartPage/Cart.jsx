import React, { Component } from 'react';
import './Cart.css';
import { withNavigate } from './withNavigate';
import { CartContext } from '../CartContext/CartContext';

class CartPage extends Component {
    static contextType = CartContext;
    constructor(props) {
        super(props);
        this.state = {
            cartItems: [],
            loading: true,
            error: null,
            summary: {
                totalQuantity: 0,
                totalPrice: 0,
                itemCount: 0
            },
            updatingItemId: null,
            notification: { show: false, message: '', type: 'success' }
        };
        this.abortController = null;
        this.cartCache = null;
        this.cartCacheTime = null;
        this.notificationTimeout = null;
    }

    componentDidMount() {
        this.loadCart();
    }

    componentWillUnmount() {
        if (this.abortController) {
            this.abortController.abort();
        }
        if (this.notificationTimeout) {
            clearTimeout(this.notificationTimeout);
        }
    }

    showNotification = (message, type = 'success') => {
        if (this.notificationTimeout) clearTimeout(this.notificationTimeout);
        
        this.setState({
            notification: { show: true, message: message, type: type }
        });
        
        this.notificationTimeout = setTimeout(() => {
            this.setState({ notification: { show: false, message: '', type: 'success' } });
        }, 3000);
    };

    getCachedCart = () => {
        if (this.cartCache && this.cartCacheTime && 
            (Date.now() - this.cartCacheTime) < 30000) { 
            console.log('Загрузка корзины из кэша памяти');
            return this.cartCache;
        }
        return null;
    };

    setCachedCart = (cartItems) => {
        this.cartCache = cartItems;
        this.cartCacheTime = Date.now();
        console.log('Корзина сохранена в кэш памяти');
    };

    getLocalStorageCart = () => {
        try {
            const userId = localStorage.getItem('userId');
            if (!userId) return null;
            
            const cacheKey = `cart_user_${userId}`;
            const cachedData = localStorage.getItem(cacheKey);
            const cacheTime = localStorage.getItem(`${cacheKey}_time`);
            
            if (cachedData && cacheTime && (Date.now() - parseInt(cacheTime)) < 60000) {
                console.log('Загрузка корзины из localStorage');
                return JSON.parse(cachedData);
            }
        } catch (error) {
            console.error('Ошибка чтения из localStorage:', error);
        }
        return null;
    };

    setLocalStorageCart = (cartItems) => {
        try {
            const userId = localStorage.getItem('userId');
            if (!userId) return;
            
            const cacheKey = `cart_user_${userId}`;
            localStorage.setItem(cacheKey, JSON.stringify(cartItems));
            localStorage.setItem(`${cacheKey}_time`, Date.now().toString());
            console.log('Корзина сохранена в localStorage');
        } catch (error) {
            console.error('Ошибка сохранения в localStorage:', error);
        }
    };

    clearCartCache = () => {
        this.cartCache = null;
        this.cartCacheTime = null;
        const userId = localStorage.getItem('userId');
        if (userId) {
            const cacheKey = `cart_user_${userId}`;
            localStorage.removeItem(cacheKey);
            localStorage.removeItem(`${cacheKey}_time`);
        }
        console.log('Кэш корзины очищен');
    };

    loadCart = async () => {
        if (this.abortController) this.abortController.abort();
        this.abortController = new AbortController();

        try {
            this.setState({ loading: true });

            const userId = localStorage.getItem('userId');
            
            if (!userId) {
                console.error('Нет userId в localStorage');
                this.setState({ loading: false, error: 'Пользователь не авторизован' });
                return;
            }

            let cachedCartItems = this.getCachedCart();

            if (!cachedCartItems) {
                cachedCartItems = this.getLocalStorageCart();
                if (cachedCartItems) {
                    this.setCachedCart(cachedCartItems);
                }
            }

            if (cachedCartItems) {
                console.log('Используем кэшированные данные корзины');
                
                this.setState({ 
                    cartItems: cachedCartItems,
                    loading: false,
                    error: null
                });
                this.calculateSummary(cachedCartItems);

                this.refreshCartInBackground();
                return;
            }

            console.log('Загружаем корзину с сервера...');
            
            const response = await fetch(`http://localhost:5214/api/Baskets/user/${userId}`, {
                signal: this.abortController.signal,
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('token')}`
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const cartItems = await response.json();
            console.log('Получены данные корзины:', cartItems);

            const formattedItems = cartItems.map(item => ({
                id: item.Id,
                userId: item.UserId,
                productId: item.ProductId,
                quantity: item.Quantity,
                product: item.Product ? {
                    id: item.Product.Id,
                    name: item.Product.Name,
                    price: item.Product.Price,
                    description: item.Product.Description,
                    isActive: item.Product.IsActive,
                    brand: item.Product.Brand ? {
                        id: item.Product.Brand.Id,
                        name: item.Product.Brand.Name
                    } : null,
                    images: item.Product.Images ? item.Product.Images.map(img => ({
                        id: img.Id,
                        imageUrl: img.ImageUrl,
                        altText: img.AltText,
                        isMain: img.IsMain
                    })) : []
                } : null
            }));
            
            console.log('Отформатированные данные:', formattedItems);

            this.setCachedCart(formattedItems);
            this.setLocalStorageCart(formattedItems);
            
            this.setState({ 
                cartItems: formattedItems,
                loading: false,
                error: null
            });
            this.calculateSummary(formattedItems);

        } catch (error) {
            if (error.name === 'AbortError') return;
            console.error('Ошибка загрузки корзины:', error);
            this.setState({ error: error.message, loading: false });
        }
    };

    refreshCartInBackground = async () => {
        if (!this.abortController || this.abortController.signal.aborted) {
            console.log('Фоновое обновление пропущено - компонент размонтирован');
            return;
        }
        
        try {
            console.log('Фоновое обновление корзины...');
            const userId = localStorage.getItem('userId');
            
            if (!userId) return;
            
            const response = await fetch(`http://localhost:5214/api/Baskets/user/${userId}`, {
                signal: this.abortController.signal,
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('token')}`
                }
            });

            if (response.ok) {
                const cartItems = await response.json();
                
                const formattedItems = cartItems.map(item => ({
                    id: item.Id,
                    userId: item.UserId,
                    productId: item.ProductId,
                    quantity: item.Quantity,
                    product: item.Product ? {
                        id: item.Product.Id,
                        name: item.Product.Name,
                        price: item.Product.Price,
                        description: item.Product.Description,
                        isActive: item.Product.IsActive,
                        brand: item.Product.Brand ? {
                            id: item.Product.Brand.Id,
                            name: item.Product.Brand.Name
                        } : null,
                        images: item.Product.Images ? item.Product.Images.map(img => ({
                            id: img.Id,
                            imageUrl: img.ImageUrl,
                            altText: img.AltText,
                            isMain: img.IsMain
                        })) : []
                    } : null
                }));

                this.setCachedCart(formattedItems);
                this.setLocalStorageCart(formattedItems);

                const currentItems = this.state.cartItems;
                if (JSON.stringify(currentItems) !== JSON.stringify(formattedItems)) {
                    console.log('Корзина обновлена из фонового запроса');
                    this.setState({ cartItems: formattedItems });
                    this.calculateSummary(formattedItems);
                }
            }
        } catch (error) {
            if (error.name === 'AbortError') {
                console.log('Фоновое обновление прервано');
                return;
            }
            console.error('Ошибка фонового обновления корзины:', error);
        }
    };

    calculateSummary = (items) => {
        const totalQuantity = items.reduce((sum, item) => sum + (item.quantity || 0), 0);
        const totalPrice = items.reduce((sum, item) => sum + ((item.product?.price || 0) * (item.quantity || 0)), 0);
        const itemCount = items.length;

        this.setState({
            summary: { totalQuantity, totalPrice, itemCount }
        });

        const cartCount = document.querySelector('.cart-count');
        if (cartCount) {
            cartCount.textContent = totalQuantity;
        }

        if (this.context && this.context.updateCartCount) {
            this.context.updateCartCount();
        }
    };

    updateQuantity = async (itemId, newQuantity) => {
        if (newQuantity < 1) {
            this.removeItem(itemId);
            return;
        }

        this.setState({ updatingItemId: itemId });

        try {
            const response = await fetch(`http://localhost:5214/api/Baskets/${itemId}`, {
                method: 'PUT',
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${localStorage.getItem('token')}`
                },
                body: JSON.stringify({ quantity: newQuantity })
            });

            if (response.ok) {
                const updatedItems = this.state.cartItems.map(item =>
                    item.id === itemId ? { ...item, quantity: newQuantity } : item
                );
                this.setState({ cartItems: updatedItems });
                this.calculateSummary(updatedItems);

                this.setCachedCart(updatedItems);
                this.setLocalStorageCart(updatedItems);
            } else {
                throw new Error('Ошибка при обновлении');
            }
        } catch (error) {
            console.error('Ошибка обновления количества:', error);
            this.showNotification('Не удалось обновить количество. Попробуйте еще раз.', 'error');
        } finally {
            this.setState({ updatingItemId: null });
        }
    };

    removeItem = async (itemId) => {
        this.setState({ updatingItemId: itemId });

        try {
            const response = await fetch(`http://localhost:5214/api/Baskets/${itemId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('token')}`
                }
            });

            if (response.ok) {
                const updatedItems = this.state.cartItems.filter(item => item.id !== itemId);
                this.setState({ cartItems: updatedItems });
                this.calculateSummary(updatedItems);

                this.setCachedCart(updatedItems);
                this.setLocalStorageCart(updatedItems);

                if (this.context && this.context.updateCartCount) {
                    this.context.updateCartCount();
                }
            } else {
                throw new Error('Ошибка при удалении');
            }
        } catch (error) {
            console.error('Ошибка удаления товара:', error);
            this.showNotification('Не удалось удалить товар. Попробуйте еще раз.', 'error');
        } finally {
            this.setState({ updatingItemId: null });
        }
    };

    clearCart = async () => {
        if (!window.confirm('Вы уверены, что хотите очистить корзину?')) return;

        this.setState({ loading: true });
        
        const result = await this.context.clearCart();
            
        if (result.success) {
            this.setState({ 
                cartItems: [], 
                loading: false 
            });
            this.calculateSummary([]);

            this.clearCartCache();
            
            this.showNotification('Корзина успешно очищена!', 'success');
        } else {
            this.setState({ loading: false });
            this.showNotification('Ошибка при очистке корзины', 'error');
        }
    };

    checkout = async () => {
        const userId = localStorage.getItem('userId');
        
        if (this.state.cartItems.length === 0) {
            this.showNotification('Корзина пуста', 'error');
            return;
        }

        try {
            const response = await fetch(`http://localhost:5214/api/Baskets/checkout/user/${userId}`, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('token')}`
                }
            });

            if (response.ok) {
                const result = await response.json();
                this.showNotification(`Заказ оформлен! ${result.message}`, 'success');

                this.setState({ cartItems: [] });
                this.calculateSummary([]);
                this.clearCartCache();
                
                if (this.context && this.context.updateCartCount) {
                    this.context.updateCartCount();
                }
            } else {
                throw new Error('Ошибка при оформлении');
            }
        } catch (error) {
            console.error('Ошибка оформления заказа:', error);
            this.showNotification('Ошибка при оформлении заказа', 'error');
        }
    };

    handleGoBack = () => {
        this.props.navigate(-1);
    };

    handleProfile = () => {
        this.props.navigate('/Profile');
    };

    handleCheckout = async () => {
        const { cartItems } = this.state;
        
        console.log('Переход в checkout, товаров в корзине:', cartItems.length);
        console.log('Данные корзины:', cartItems);
        
        if (cartItems.length === 0) {
            this.showNotification('Корзина пуста', 'error');
            return;
        }

        await new Promise(resolve => setTimeout(resolve, 100));

        let finalCartItems = cartItems;
        if (this.context && this.context.state && this.context.state.cartItems && this.context.state.cartItems.length > 0) {
            finalCartItems = this.context.state.cartItems;
            console.log('Используем корзину из контекста:', finalCartItems.length);
        }
        
        localStorage.setItem('needRefreshProfile', 'true');
        console.log('Установлен флаг обновления профиля из корзины');

        const cartItemsCopy = JSON.parse(JSON.stringify(finalCartItems));
        
        this.props.navigate('/checkout', { 
            state: { cartItems: cartItemsCopy } 
        });
    };

    handleSearch = (e) => {
        e.preventDefault();

        const input = e.target.querySelector('input[type="search"]')
        const query = input?.value;
        if (query && query.trim()) {
            this.props.navigate(`/search?q=${encodeURIComponent(query.trim())}`);
        }
    }

    formatPrice = (price) => {
        if (price === undefined || price === null || isNaN(price)) {
            return '0 ₽';
        }
        return `${Number(price).toLocaleString('ru-RU')} ₽`;
    };

    render() {
        const { cartItems, loading, error, summary, updatingItemId, notification } = this.state;

        console.log('Рендер корзины, количество товаров:', cartItems.length);

        if (loading) {
            return (
                <div className="cart-page">
                    <div className="loading-spinner">
                        <div className="spinner"></div>
                        <p>Загрузка корзины...</p>
                    </div>
                </div>
            );
        }

        if (error) {
            return (
                <div className="cart-page">
                    <div className="error-message">
                        <p>{error}</p>
                        <button onClick={this.loadCart} className="retry-btn">Повторить</button>
                    </div>
                </div>
            );
        }

        return (
            <div className="cart-page">
                {notification.show && (
                    <div className={`cart-notification ${notification.type === 'error' ? 'cart-notification-error' : ''}`}>
                        <div className="cart-notification-content">
                            <div className="cart-notification-text">
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
                                <form onSubmit={this.handleSearch}>
                                    <input
                                        type="search"
                                        placeholder="Поиск товаров..."
                                        className="header-search-block__search"
                                    />
                                </form>
                            </div>
                        </div>

                        <div className="header-icons">
                            <button className="icon-btn user-icon white-icon" onClick={this.handleProfile}>
                                <svg width="20" height="20" viewBox="0 0 24 24" fill="black">
                                    <path d="M12 12C14.7614 12 17 9.76142 17 7C17 4.23858 14.7614 2 12 2C9.23858 2 7 4.23858 7 7C7 9.76142 9.23858 12 12 12Z" stroke="currentColor" strokeWidth="2"/>
                                    <path d="M20 22C20 17.5817 16.4183 14 12 14C7.58172 14 4 17.5817 4 22" stroke="currentColor" strokeWidth="2"/>
                                </svg>
                            </button>
                            <button className="icon-btn cart-icon white-icon" onClick={() => this.props.navigate('/cart')}>
                                <svg width="20" height="20" viewBox="0 0 24 24" fill='black'>
                                    <path d="M9 22C9.55228 22 10 21.5523 10 21C10 20.4477 9.55228 20 9 20C8.44772 20 8 20.4477 8 21C8 21.5523 8.44772 22 9 22Z" stroke="currentColor" strokeWidth="2"/>
                                    <path d="M20 22C20.5523 22 21 21.5523 21 21C21 20.4477 20.5523 20 20 20C19.4477 20 19 20.4477 19 21C19 21.5523 19.4477 22 20 22Z" stroke="currentColor" strokeWidth="2"/>
                                    <path d="M1 1H5L7.68 14.39C7.77144 14.8504 8.02191 15.264 8.38755 15.5583C8.75318 15.8526 9.2107 16.009 9.68 16H19.4C19.8693 16.009 20.3268 15.8526 20.6925 15.5583C21.0581 15.264 21.3086 14.8504 21.4 14.39L23 6H6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                                </svg>
                                <span className="cart-count">{summary.totalQuantity}</span>
                            </button>
                        </div>
                    </nav>
                </header>

                <main className="main-content">
                    <div className="container cart-container">
                        <div className="cart-header">
                            <h1 className="page-title">Корзина</h1>
                            <button className="back-button" onClick={this.handleGoBack}>
                                Назад
                            </button>
                        </div>

                        {cartItems.length === 0 ? (
                            <div className="empty-cart">
                                <h2>Ваша корзина пуста</h2>
                                <p>Добавьте товары из каталога, чтобы оформить заказ</p>
                                <button className="continue-shopping" onClick={() => this.props.navigate('/HomePage')}>
                                    Продолжить покупки
                                </button>
                            </div>
                        ) : (
                            <div className="cart-content">
                                <div className="cart-items">
                                    {cartItems.map((item) => {
                                        const product = item.product;
                                        const price = product?.price || 0;
                                        const quantity = item.quantity || 1;
                                        const totalPrice = price * quantity;
                                        
                                        // Получаем URL изображения и добавляем base URL
                                        let imageUrl = product?.images?.[0]?.imageUrl;
                                        if (imageUrl && !imageUrl.startsWith('http')) {
                                            imageUrl = `http://localhost:5214${imageUrl.startsWith('/') ? '' : '/'}${imageUrl}`;
                                        }

                                        return (
                                            <div key={item.id} className="cart-item">
                                                <div className="cart-item-image">
                                                    {imageUrl ? (
                                                        <img 
                                                            src={imageUrl} 
                                                            alt={product?.name}
                                                            onError={(e) => {
                                                                console.error('Ошибка загрузки изображения в корзине:', imageUrl);
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
                                                
                                                <div className="cart-item-info">
                                                    <h3 className="cart-item-title">{product?.name || 'Товар'}</h3>
                                                    <div className="cart-item-brand">
                                                        Бренд: {product?.brand?.name || 'Не указан'}
                                                    </div>
                                                    <div className="cart-item-price">
                                                        {this.formatPrice(price)} / шт.
                                                    </div>
                                                </div>
                                                
                                                <div className="cart-item-quantity">
                                                    <button 
                                                        className="quantity-btn minus"
                                                        onClick={() => this.updateQuantity(item.id, quantity - 1)}
                                                        disabled={updatingItemId === item.id || quantity <= 1}
                                                    >
                                                        -
                                                    </button>
                                                    <span className="quantity-value">{quantity}</span>
                                                    <button 
                                                        className="quantity-btn plus"
                                                        onClick={() => this.updateQuantity(item.id, quantity + 1)}
                                                        disabled={updatingItemId === item.id}
                                                    >
                                                        +
                                                    </button>
                                                </div>
                                                
                                                <div className="cart-item-total">
                                                    {this.formatPrice(totalPrice)}
                                                </div>
                                            </div>
                                        );
                                    })}
                                </div>

                                <div className="cart-summary">
                                    <h3 className="summary-title">Итого</h3>
                                    <div className="summary-row">
                                        <span>Товары ({summary.itemCount} шт.)</span>
                                        <span>{this.formatPrice(summary.totalPrice)}</span>
                                    </div>
                                    <div className="summary-row delivery">
                                        <span>Доставка</span>
                                        <span className="free">Бесплатно</span>
                                    </div>
                                    <div className="summary-total">
                                        <span>К оплате</span>
                                        <span className="total-price">{this.formatPrice(summary.totalPrice)}</span>
                                    </div>
                                    
                                    <div className="cart-actions">
                                        <button className="clear-cart-btn" onClick={this.clearCart}>
                                            Очистить корзину
                                        </button>
                                        <button className="checkout-btn" onClick={this.handleCheckout}>
                                            Оформить заказ
                                        </button>
                                    </div>
                                </div>
                            </div>
                        )}
                    </div>
                </main>
            </div>
        );
    }
}

export default withNavigate(CartPage);