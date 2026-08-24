import React, { Component } from 'react';
import { useParams } from 'react-router-dom';
import './ProductPage.css';
import { withNavigate } from './withNavigate';
import CartIndicator from '../CartIndicator/CartIndicator';
import { CartContext } from '../CartContext/CartContext';

function withParams(Component) {
    return function WrappedComponent(props) {
        const params = useParams();
        return <Component {...props} params={params} />;
    }
}

class ProductPage extends Component {
    static contextType = CartContext;
    constructor(props) {
        super(props);
        this.state = {
            product: null,
            loading: true,
            error: null,
            quantity: 1,
            selectedImage: null,
            isAddingToCart: false,
            notification: { show: false, message: '', productName: '' },
            reviews: [],
            reviewsLoading: false,
            averageRating: 0,
            totalReviews: 0,
            newReview: { rating: 5, comment: '' },
            reviewSubmitting: false,
            editingReview: null,
            editComment: '',
            editRating: 5,
            editSubmitting: false,
            successMessage: null,
            errorMessage: null
        };
        this.abortController = null;
        this.productCache = new Map();
        this.notificationTimeout = null;
        this.messageTimeout = null;
    }

    componentDidMount() {
        const { id } = this.props.params || {};
        console.log('ProductPage загружен, ID из URL:', id);
        if (id) {
            this.fetchProduct(id);
        } else {
            this.setState({ 
                error: 'ID товара не указан', 
                loading: false 
            });
        }
    }

    componentWillUnmount() {
        if (this.abortController) {
            this.abortController.abort();
        }
        if (this.notificationTimeout) {
            clearTimeout(this.notificationTimeout);
        }
        if (this.messageTimeout) {
            clearTimeout(this.messageTimeout);
        }
    }

    showSuccessMessage = (message) => {
        this.setState({ successMessage: message, errorMessage: null });
        if (this.messageTimeout) clearTimeout(this.messageTimeout);
        this.messageTimeout = setTimeout(() => {
            this.setState({ successMessage: null });
        }, 3000);
    };

    showErrorMessage = (message) => {
        this.setState({ errorMessage: message, successMessage: null });
        if (this.messageTimeout) clearTimeout(this.messageTimeout);
        this.messageTimeout = setTimeout(() => {
            this.setState({ errorMessage: null });
        }, 3000);
    };

    showNotification = (productName, quantity) => {
        if (this.notificationTimeout) clearTimeout(this.notificationTimeout);
        
        this.setState({
            notification: { 
                show: true, 
                message: 'Товар добавлен в корзину!', 
                productName: `${productName} (${quantity} шт.)`
            }
        });
        
        this.notificationTimeout = setTimeout(() => {
            this.setState({ notification: { show: false, message: '', productName: '' } });
        }, 3000);
    };

    loadReviews = async (productId) => {
        console.log('Загрузка отзывов для товара ID:', productId);
        this.setState({ reviewsLoading: true });
        try {
            const response = await fetch(`http://localhost:5214/api/Reviews/product/${productId}`);
            
            if (response.ok) {
                const data = await response.json();
                console.log('Полученные данные отзывов:', data);
                
                const reviewsData = data.reviews || data.Reviews || data.items || [];
                console.log('reviewsData:', reviewsData);
                console.log('reviewsData длина:', reviewsData.length);
                
                let reviewsArray = [];
                if (Array.isArray(reviewsData)) {
                    reviewsArray = reviewsData;
                } else if (reviewsData && typeof reviewsData === 'object') {
                    reviewsArray = Object.values(reviewsData);
                }
                
                const normalizedReviews = reviewsArray.map(review => ({
                    id: review.Id || review.id,
                    comment: review.Comment || review.comment,
                    rating: review.Rating || review.rating,
                    userName: review.UserName || review.userName || 'Аноним',
                    userId: review.UserId || review.userId,
                    createdAt: review.CreatedAt || review.createdAt,
                    isApproved: review.IsApproved || review.isApproved
                }));
                
                console.log('Нормализованные отзывы:', normalizedReviews);
                
                this.setState({
                    reviews: normalizedReviews,
                    averageRating: data.averageRating || data.AverageRating || 0,
                    totalReviews: data.totalReviews || data.TotalReviews || 0,
                    reviewsLoading: false
                });
            } else {
                this.setState({ reviewsLoading: false });
            }
        } catch (error) {
            console.error('Ошибка загрузки отзывов:', error);
            this.setState({ reviewsLoading: false });
        }
    };

    loadMainImage = async (productId) => {
        console.log('loadMainImage вызван для товара ID:', productId);
        try {
            const response = await fetch(`http://localhost:5214/api/ProductImages/product/${productId}/main`);
            console.log('Статус ответа loadMainImage:', response.status);
            
            if (response.ok) {
                const image = await response.json();
                console.log('Получено главное изображение:', image);
                if (image && image.imageUrl) {
                    let imageUrl = image.imageUrl;
                    if (imageUrl && !imageUrl.startsWith('http')) {
                        imageUrl = `http://localhost:5214${imageUrl.startsWith('/') ? '' : '/'}${imageUrl}`;
                    }
                    console.log('Устанавливаем selectedImage (исправленный URL):', imageUrl);
                    this.setState({ selectedImage: imageUrl });
                } else {
                    console.log('Нет imageUrl в ответе');
                }
            } else {
                console.log('Ответ не OK, статус:', response.status);
            }
        } catch (error) {
            console.error('Ошибка загрузки главного изображения:', error);
        }
    };

    startEditReview = (review) => {
        this.setState({
            editingReview: review,
            editComment: review.comment,
            editRating: review.rating
        });
    };

    cancelEditReview = () => {
        this.setState({
            editingReview: null,
            editComment: '',
            editRating: 5
        });
    };

    handleEditRatingChange = (rating) => {
        this.setState({ editRating: rating });
    };

    handleEditCommentChange = (e) => {
        this.setState({ editComment: e.target.value });
    };

    updateReview = async () => {
        const { editingReview, editComment, editRating } = this.state;
        const token = localStorage.getItem('token');

        if (!editComment.trim()) {
            this.showErrorMessage('Пожалуйста, введите текст отзыва');
            return;
        }

        if (editRating < 1 || editRating > 5) {
            this.showErrorMessage('Оценка должна быть от 1 до 5');
            return;
        }

        this.setState({ editSubmitting: true });

        try {
            const response = await fetch(`http://localhost:5214/api/Reviews/${editingReview.id}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({
                    comment: editComment,
                    rating: editRating,
                    isApproved: true
                })
            });

            if (response.ok) {
                this.showSuccessMessage('Отзыв успешно обновлен!');
                this.cancelEditReview();
                await this.loadReviews(this.state.product.Id);
                localStorage.setItem('needRefreshProfile', 'true');
            } else {
                const error = await response.json();
                this.showErrorMessage(error.message || 'Ошибка при обновлении отзыва');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.showErrorMessage('Ошибка при обновлении отзыв');
        } finally {
            this.setState({ editSubmitting: false });
        }
    };

    deleteReview = async (reviewId) => {
        const token = localStorage.getItem('token');
        const userId = localStorage.getItem('userId');

        if (!userId || !token) {
            this.showErrorMessage('Пожалуйста, войдите в систему');
            return;
        }

        if (!window.confirm('Вы уверены, что хотите удалить этот отзыв?')) {
            return;
        }

        try {
            const response = await fetch(`http://localhost:5214/api/Reviews/${reviewId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (response.ok) {
                this.showSuccessMessage('Отзыв успешно удален!');
                await this.loadReviews(this.state.product.Id);
                localStorage.setItem('needRefreshProfile', 'true');
            } else {
                const error = await response.json();
                this.showErrorMessage(error.message || 'Ошибка при удалении отзыва');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.showErrorMessage('Ошибка при удалении отзыва');
        }
    };

    submitReview = async () => {
        const { newReview, product } = this.state;
        const token = localStorage.getItem('token');
        const userId = localStorage.getItem('userId');

        if (!userId || !token) {
            this.showErrorMessage('Пожалуйста, войдите в систему, чтобы оставить отзыв');
            return;
        }

        if (!newReview.comment.trim()) {
            this.showErrorMessage('Пожалуйста, введите текст отзыва');
            return;
        }

        if (newReview.rating < 1 || newReview.rating > 5) {
            this.showErrorMessage('Оценка должна быть от 1 до 5');
            return;
        }

        this.setState({ reviewSubmitting: true });

        try {
            const response = await fetch('http://localhost:5214/api/Reviews', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({
                    productId: product.Id,
                    comment: newReview.comment,
                    rating: newReview.rating
                })
            });

            const result = await response.json();
            
            if (response.ok && result.success) {
                this.showSuccessMessage(result.message || 'Отзыв успешно добавлен!');
                
                this.setState({
                    newReview: { rating: 5, comment: '' },
                    reviewSubmitting: false
                });
                
                await this.loadReviews(product.Id);
                localStorage.setItem('needRefreshProfile', 'true');
                
            } else {
                this.showErrorMessage(result.message || 'Ошибка при отправке отзыва');
                this.setState({ reviewSubmitting: false });
            }
        } catch (error) {
            console.error('Ошибка:', error);
            this.showErrorMessage('Ошибка при отправке отзыва');
            this.setState({ reviewSubmitting: false });
        }
    };

    handleRatingChange = (rating) => {
        this.setState({
            newReview: { ...this.state.newReview, rating }
        });
    };

    handleReviewCommentChange = (e) => {
        this.setState({
            newReview: { ...this.state.newReview, comment: e.target.value }
        });
    };

    getCachedProduct = (productId) => {
        const cached = this.productCache.get(productId);
        if (cached && (Date.now() - cached.timestamp) < 300000) {
            console.log(`Загрузка товара ${productId} из кэша`);
            return cached.data;
        }
        return null;
    };

    setCachedProduct = (productId, data) => {
        this.productCache.set(productId, {
            data: data,
            timestamp: Date.now()
        });
        console.log(`Товар ${productId} сохранен в кэш`);
    };

    getLocalStorageProduct = (productId) => {
        try {
            const cacheKey = `product_${productId}`;
            const cachedData = localStorage.getItem(cacheKey);
            const cacheTime = localStorage.getItem(`${cacheKey}_time`);
            
            if (cachedData && cacheTime && (Date.now() - parseInt(cacheTime)) < 300000) {
                console.log(`Загрузка товара ${productId} из localStorage`);
                return JSON.parse(cachedData);
            }
        } catch (error) {
            console.error('Ошибка чтения из localStorage:', error);
        }
        return null;
    };

    setLocalStorageProduct = (productId, data) => {
        try {
            const cacheKey = `product_${productId}`;
            localStorage.setItem(cacheKey, JSON.stringify(data));
            localStorage.setItem(`${cacheKey}_time`, Date.now().toString());
            console.log(`Товар ${productId} сохранен в localStorage`);
        } catch (error) {
            console.error('Ошибка сохранения в localStorage:', error);
        }
    };

    preloadSimilarProducts = async (categoryId, currentProductId) => {
        if (!categoryId) return;
        
        try {
            const cacheKey = `category_${categoryId}_products`;
            const cachedData = localStorage.getItem(cacheKey);
            const cacheTime = localStorage.getItem(`${cacheKey}_time`);
            
            if (cachedData && cacheTime && (Date.now() - parseInt(cacheTime)) < 300000) {
                console.log('Похожие товары загружены из кэша');
                return;
            }
            
            console.log('Предзагрузка похожих товаров...');
            const response = await fetch(`http://localhost:5214/api/Productss/category/${categoryId}`, {
                signal: this.abortController?.signal
            });
            
            if (response.ok) {
                const products = await response.json();
                const limitedProducts = products.slice(0, 4);
                localStorage.setItem(cacheKey, JSON.stringify(limitedProducts));
                localStorage.setItem(`${cacheKey}_time`, Date.now().toString());
                console.log('Похожие товары сохранены в кэш');
            }
        } catch (error) {
            console.error('Ошибка предзагрузки похожих товаров:', error);
        }
    };

    getMainImage = () => {
        const { product, selectedImage } = this.state;
        const images = product?.Images || [];
        
        console.log('getMainImage - selectedImage:', selectedImage);
        console.log('getMainImage - images:', images);
        console.log('getMainImage - images.length:', images.length);

        const fixImageUrl = (url) => {
            if (!url) return null;
            if (url.startsWith('http')) return url;
            return `http://localhost:5214${url.startsWith('/') ? '' : '/'}${url}`;
        };
        
        if (selectedImage) {
            const fixedUrl = fixImageUrl(selectedImage);
            console.log('Возвращаем выбранное изображение (исправленное):', fixedUrl);
            return fixedUrl;
        }
        
        if (images.length > 0) {
            const mainImg = images.find(img => img.isMain || img.IsMain);
            if (mainImg) {
                const imgUrl = mainImg.imageUrl || mainImg.ImageUrl;
                const fixedUrl = fixImageUrl(imgUrl);
                console.log('Возвращаем главное изображение (mainImg):', fixedUrl);
                return fixedUrl;
            }
            const firstImgUrl = images[0].imageUrl || images[0].ImageUrl;
            const fixedUrl = fixImageUrl(firstImgUrl);
            console.log('Возвращаем первое изображение:', fixedUrl);
            return fixedUrl;
        }
        
        if (product?.Id) {
            console.log('Нет изображений, вызываем loadMainImage для ID:', product.Id);
            this.loadMainImage(product.Id);
        }
        
        console.log('Возвращаем null (нет изображений)');
        return null;
    };

    fetchProduct = async (productId) => {
        if (this.abortController) this.abortController.abort();
        this.abortController = new AbortController();

        try {
            this.setState({ loading: true });

            let product = this.getCachedProduct(productId);

            if (!product) {
                product = this.getLocalStorageProduct(productId);
            }

            if (product) {
                console.log('Используем кэшированные данные товара');
                
                let mainImage = null;
                if (product.Images && product.Images.length > 0) {
                    const mainImg = product.Images.find(img => img.isMain || img.IsMain);
                    mainImage = mainImg ? (mainImg.imageUrl || mainImg.ImageUrl) : (product.Images[0].imageUrl || product.Images[0].ImageUrl);
                }

                this.setState({ 
                    product: product, 
                    loading: false, 
                    error: null,
                    selectedImage: mainImage
                });

                await this.loadReviews(productId);
                this.refreshProductInBackground(productId);
                return;
            }

            console.log('Запрос к API:', `http://localhost:5214/api/Productss/${productId}`);
            
            const response = await fetch(`http://localhost:5214/api/Productss/${productId}`, {
                signal: this.abortController.signal
            });

            console.log('Статус ответа:', response.status);

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            product = await response.json();
            
            console.log('Товар загружен. Полная структура:');
            console.log(JSON.stringify(product, null, 2));
            console.log('product.Images из ответа:', product.Images);

            if (!product.Images || product.Images.length === 0) {
                console.log('Нет изображений в product.Images, загружаем отдельно...');
                try {
                    const imagesResponse = await fetch(`http://localhost:5214/api/ProductImages/product/${productId}`);
                    console.log('Статус запроса изображений:', imagesResponse.status);
                    
                    if (imagesResponse.ok) {
                        const images = await imagesResponse.json();
                        console.log('Изображения получены отдельно:', images);
                        product.Images = images.map(img => ({
                            id: img.id,
                            imageUrl: img.imageUrl,
                            altText: img.altText,
                            isMain: img.isMain
                        }));
                        console.log('product.Images после обработки:', product.Images);
                    } else {
                        console.log('Ошибка при загрузке изображений, статус:', imagesResponse.status);
                    }
                } catch (imgError) {
                    console.error('Ошибка загрузки изображений:', imgError);
                }
            }

            this.setCachedProduct(productId, product);
            this.setLocalStorageProduct(productId, product);

            let mainImage = null;
            if (product.Images && product.Images.length > 0) {
                const mainImg = product.Images.find(img => img.isMain || img.IsMain);
                mainImage = mainImg ? (mainImg.imageUrl || mainImg.ImageUrl) : (product.Images[0].imageUrl || product.Images[0].ImageUrl);
                console.log('mainImage установлено:', mainImage);
            }

            this.setState({ 
                product: product, 
                loading: false, 
                error: null,
                selectedImage: mainImage
            });

            await this.loadReviews(productId);

            if (product.CategoryId) {
                this.preloadSimilarProducts(product.CategoryId, productId);
            }

        } catch (error) {
            if (error.name === 'AbortError') return;
            console.error('ОШИБКА загрузки товара:', error);
            this.setState({ error: error.message, loading: false });
        }
    };

    refreshProductInBackground = async (productId) => {
        try {
            if (!this.abortController || this.abortController.signal.aborted) {
                console.log('Фоновое обновление пропущено - компонент размонтирован');
                return;
            }
            
            console.log('Фоновое обновление товара...');
            const response = await fetch(`http://localhost:5214/api/Productss/${productId}`, {
                signal: this.abortController?.signal
            });
            
            if (response.ok) {
                const freshProduct = await response.json();

                this.setCachedProduct(productId, freshProduct);
                this.setLocalStorageProduct(productId, freshProduct);

                const currentProduct = this.state.product;
                if (currentProduct && JSON.stringify(currentProduct) !== JSON.stringify(freshProduct)) {
                    console.log('Товар обновлен из фонового запроса');
                    
                    let mainImage = null;
                    if (freshProduct.Images && freshProduct.Images.length > 0) {
                        const mainImg = freshProduct.Images.find(img => img.isMain || img.IsMain);
                        mainImage = mainImg ? (mainImg.imageUrl || mainImg.ImageUrl) : (freshProduct.Images[0].imageUrl || freshProduct.Images[0].ImageUrl);
                    }
                    
                    this.setState({ 
                        product: freshProduct,
                        selectedImage: mainImage
                    });
                    
                    await this.loadReviews(productId);
                }
            }
        } catch (error) {
            if (error.name === 'AbortError') {
                console.log('Фоновое обновление прервано');
                return;
            }
            console.error('Ошибка фонового обновления:', error);
        }
    };

    clearProductCache = () => {
        this.productCache.clear();
        console.log('Кэш товаров в памяти очищен');
    };

    clearAllCache = () => {
        this.clearProductCache();
        const keys = Object.keys(localStorage);
        keys.forEach(key => {
            if (key.startsWith('product_') || key.startsWith('category_')) {
                localStorage.removeItem(key);
                localStorage.removeItem(`${key}_time`);
            }
        });
        console.log('Весь кэш товаров очищен');
    };

    handleQuantityChange = (delta) => {
        this.setState(prevState => ({
            quantity: Math.max(1, prevState.quantity + delta)
        }));
    };

    handleQuantityInput = (e) => {
        const value = parseInt(e.target.value);
        if (!isNaN(value) && value > 0) {
            this.setState({ quantity: value });
        }
    };

    handleAddToCart = async () => {
        const { product, quantity } = this.state;
        
        if (!product || !product.Id) {
            this.showErrorMessage('Ошибка: товар не найден');
            return;
        }

        this.setState({ isAddingToCart: true });
        
        const result = await this.context.addToCart(product.Id, quantity);
        
        this.setState({ isAddingToCart: false });
        
        if (result.success) {
            this.showNotification(product.Name, quantity);
        } else {
            this.showErrorMessage('Ошибка при добавлении в корзину');
        }
    };

    handleGoBack = () => {
        this.props.navigate(-1);
    };

    handleProfile = () => {
        this.props.navigate('/Profile');
    };

    handleCartClick = () => {
        this.props.navigate('/cart');
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
        const { product, loading, error, quantity, selectedImage, isAddingToCart, notification, reviews, reviewsLoading, averageRating, totalReviews, newReview, reviewSubmitting, editingReview, editComment, editRating, editSubmitting, successMessage, errorMessage } = this.state;

        console.log('Рендер ProductPage, selectedImage:', selectedImage);
        console.log('product?.Images:', product?.Images);

        if (loading) {
            return (
                <div className="product-page">
                    <div className="loading-spinner">
                        <div className="spinner"></div>
                        <p>Загрузка товара...</p>
                    </div>
                </div>
            );
        }

        if (error || !product) {
            return (
                <div className="product-page">
                    <div className="error-message">
                        <p>{error || 'Товар не найден'}</p>
                        <button onClick={this.handleGoBack} className="retry-btn">
                            Вернуться назад
                        </button>
                    </div>
                </div>
            );
        }

        const images = product.Images || [];
        const mainImage = this.getMainImage();

        console.log('mainImage из getMainImage():', mainImage);
        console.log('images массив:', images);

        const hasValues = product.Values && 
                         Array.isArray(product.Values) && 
                         product.Values.length > 0;

        return (
            <div className="product-page">
                {successMessage && (
                    <div className="message-notification success">
                        <div className="message-content">
                            <span className="message-icon">✓</span>
                            <span className="message-text">{successMessage}</span>
                        </div>
                    </div>
                )}
                {errorMessage && (
                    <div className="message-notification error">
                        <div className="message-content">
                            <span className="message-icon">⚠</span>
                            <span className="message-text">{errorMessage}</span>
                        </div>
                    </div>
                )}

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
                    <div className="container product-container">
                        <div className="product-header">
                            <h1 className="page-title">Карточка товара</h1>
                            <button className="back-button" onClick={this.handleGoBack}>
                                Назад
                            </button>
                        </div>

                        <div className="product-main">
                            <div className="product-gallery">
                                <div className="main-image">
                                    {mainImage ? (
                                        <img 
                                            src={mainImage} 
                                            alt={product.Name}
                                            onError={(e) => {
                                                console.error('Ошибка загрузки изображения:', mainImage);
                                                e.target.onerror = null;
                                                e.target.src = 'https://via.placeholder.com/400x400?text=No+Image';
                                            }}
                                            onLoad={() => console.log('Изображение загружено:', mainImage)}
                                        />
                                    ) : (
                                        <div className="no-image-placeholder">
                                            <span>Нет изображения</span>
                                        </div>
                                    )}
                                </div>
                                
                                {images && images.length > 1 && (
                                    <div className="image-thumbnails">
                                        {images.map((img, index) => {
                                            let imgUrl = img.imageUrl || img.ImageUrl;
                                            if (imgUrl && !imgUrl.startsWith('http')) {
                                                imgUrl = `http://localhost:5214${imgUrl.startsWith('/') ? '' : '/'}${imgUrl}`;
                                            }
                                            return (
                                                <div 
                                                    key={img.id || index}
                                                    className={`thumbnail ${selectedImage === imgUrl ? 'active' : ''}`}
                                                    onClick={() => this.setState({ selectedImage: imgUrl })}
                                                >
                                                    <img 
                                                        src={imgUrl} 
                                                        alt={`${product.Name} ${index + 1}`}
                                                        onError={(e) => {
                                                            console.error('Ошибка загрузки миниатюры:', imgUrl);
                                                            e.target.onerror = null;
                                                            e.target.style.display = 'none';
                                                        }}
                                                    />
                                                </div>
                                            );
                                        })}
                                    </div>
                                )}
                            </div>

                            <div className="product-info">
                                <h2 className="product-title">{product.Name}</h2>
                                
                                {product.Brand && (
                                    <div className="product-brand">
                                        <span className="brand-label">Бренд:</span>
                                        <span className="brand-value">{product.Brand.Name}</span>
                                    </div>
                                )}

                                <div className="product-price-section">
                                    <div className="price-block">
                                        <span className="current-price">
                                            {product.Price?.toLocaleString('ru-RU')} ₽
                                        </span>
                                    </div>
                                    
                                    <div className="availability">
                                        {product.IsActive ? (
                                            <span className="in-stock-badge">В наличии</span>
                                        ) : (
                                            <span className="out-of-stock-badge">Нет в наличии</span>
                                        )}
                                    </div>
                                </div>

                                <div className="product-specs">
                                    <h3 className="specs-title">Характеристики:</h3>
                                    
                                    {hasValues ? (
                                        <div className="specs-grid">
                                            {product.Values.map((value) => (
                                                <div key={value.Id} className="spec-row">
                                                    <span className="spec-name">
                                                        {value.Attribute?.Name || 'Характеристика'}:
                                                    </span>
                                                    <span className="spec-value">
                                                        {value.Value} {value.Attribute?.Unit || ''}
                                                    </span>
                                                </div>
                                            ))}
                                        </div>
                                    ) : (
                                        <div className="no-specs">
                                            <p>Характеристики отсутствуют</p>
                                        </div>
                                    )}
                                </div>

                                <div className="cart-block">
                                    <div className="cart-header">
                                        <span className="cart-title">Количество</span>
                                        <span className="cart-total">
                                            Итого: {(product.Price * quantity).toLocaleString('ru-RU')} ₽
                                        </span>
                                    </div>
                                    
                                    <div className="cart-controls">
                                        <div className="quantity-control">
                                            <button 
                                                className="quantity-btn minus"
                                                onClick={() => this.handleQuantityChange(-1)}
                                                disabled={quantity <= 1 || isAddingToCart}
                                            >
                                                <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                                                    <path d="M5 12H19" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
                                                </svg>
                                            </button>
                                            
                                            <input 
                                                type="number" 
                                                className="quantity-input"
                                                value={quantity} 
                                                onChange={this.handleQuantityInput}
                                                min="1"
                                                disabled={isAddingToCart}
                                            />
                                            
                                            <button 
                                                className="quantity-btn plus"
                                                onClick={() => this.handleQuantityChange(1)}
                                                disabled={isAddingToCart}
                                            >
                                                <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                                                    <path d="M12 5V19M5 12H19" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
                                                </svg>
                                            </button>
                                        </div>
                                        
                                        <button 
                                            className={`add-to-cart-btn ${isAddingToCart ? 'adding' : ''}`}
                                            onClick={this.handleAddToCart}
                                            disabled={!product.IsActive || isAddingToCart}
                                        >
                                            {isAddingToCart ? (
                                                <>
                                                    <span className="spinner-small"></span>
                                                    Добавление...
                                                </>
                                            ) : (
                                                <>
                                                    В корзину
                                                </>
                                            )}
                                        </button>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div className="product-reviews">
                            <h2 className="reviews-title">
                                Отзывы 
                                {totalReviews > 0 && (
                                    <span className="average-rating">⭐ {averageRating} ({totalReviews} отзывов)</span>
                                )}
                            </h2>

                            <div className="add-review">
                                <h3>Оставить отзыв</h3>
                                <div className="rating-input">
                                    <span>Ваша оценка: </span>
                                    {[1, 2, 3, 4, 5].map(star => (
                                        <span 
                                            key={star}
                                            className={`star ${newReview.rating >= star ? 'active' : ''}`}
                                            onClick={() => this.handleRatingChange(star)}
                                        >
                                            ★
                                        </span>
                                    ))}
                                </div>
                                <textarea
                                    className="review-textarea"
                                    placeholder="Поделитесь своим мнением о товаре..."
                                    value={newReview.comment}
                                    onChange={this.handleReviewCommentChange}
                                    rows="4"
                                />
                                <button 
                                    className="submit-review-btn"
                                    onClick={this.submitReview}
                                    disabled={reviewSubmitting}
                                >
                                    {reviewSubmitting ? 'Отправка...' : 'Отправить отзыв'}
                                </button>
                            </div>

                            {reviewsLoading ? (
                                <div className="reviews-loading">Загрузка отзывов...</div>
                            ) : reviews.length > 0 ? (
                                <div className="reviews-list">
                                    {reviews.map(review => {
                                        const currentUserId = localStorage.getItem('userId');
                                        const userRole = localStorage.getItem('userRole');
                                        const canEditDelete = currentUserId === review.userId?.toString() || userRole === 'Admin';
                                        
                                        if (editingReview && editingReview.id === review.id) {
                                            return (
                                                <div key={review.id} className="review-item editing">
                                                    <div className="review-header">
                                                        <span className="review-author">{review.userName || 'Аноним'}</span>
                                                        <div className="rating-input edit-rating">
                                                            {[1, 2, 3, 4, 5].map(star => (
                                                                <span 
                                                                    key={star}
                                                                    className={`star ${editRating >= star ? 'active' : ''}`}
                                                                    onClick={() => this.handleEditRatingChange(star)}
                                                                >
                                                                    ★
                                                                </span>
                                                            ))}
                                                        </div>
                                                    </div>
                                                    <textarea
                                                        className="edit-review-textarea"
                                                        value={editComment}
                                                        onChange={this.handleEditCommentChange}
                                                        rows="3"
                                                    />
                                                    <div className="edit-review-actions">
                                                        <button 
                                                            className="save-edit-btn"
                                                            onClick={this.updateReview}
                                                            disabled={editSubmitting}
                                                        >
                                                            {editSubmitting ? 'Сохранение...' : 'Сохранить'}
                                                        </button>
                                                        <button 
                                                            className="cancel-edit-btn"
                                                            onClick={this.cancelEditReview}
                                                        >
                                                            Отмена
                                                        </button>
                                                    </div>
                                                </div>
                                            );
                                        }
                                        
                                        return (
                                            <div key={review.id} className="review-item">
                                                <div className="review-header">
                                                    <span className="review-author">{review.userName || 'Аноним'}</span>
                                                    <span className="review-rating">
                                                        {"★".repeat(review.rating)}{"☆".repeat(5 - review.rating)}
                                                    </span>
                                                    <span className="review-date">
                                                        {review.createdAt 
                                                            ? new Date(review.createdAt).toLocaleDateString('ru-RU') 
                                                            : 'Дата не указана'}
                                                    </span>
                                                    {canEditDelete && (
                                                        <div className="review-actions">
                                                            <button 
                                                                className="edit-review-btn"
                                                                onClick={() => this.startEditReview(review)}
                                                                title="Редактировать отзыв"
                                                            >
                                                                Редактировать 
                                                            </button>
                                                            <button 
                                                                className="delete-review-btn"
                                                                onClick={() => this.deleteReview(review.id)}
                                                                title="Удалить отзыв"
                                                            >
                                                                Удалить 
                                                            </button>
                                                        </div>
                                                    )}
                                                </div>
                                                <p className="review-comment">{review.comment}</p>
                                            </div>
                                        );
                                    })}
                                </div>
                            ) : (
                                <p className="no-reviews">Отзывов пока нет. Будьте первым!</p>
                            )}
                        </div>
                    </div>
                </main>
            </div>
        );
    }
}

export default withNavigate(withParams(ProductPage));