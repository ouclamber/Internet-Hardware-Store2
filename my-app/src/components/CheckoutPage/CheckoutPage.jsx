import React, { Component } from 'react';
import './CheckoutPage.css';
import { withNavigate } from './withNavigate';
import CartIndicator from '../CartIndicator/CartIndicator';
import { CartContext } from '../CartContext/CartContext';

class CheckoutPage extends Component {
    static contextType = CartContext;
    constructor(props) {
        super(props);
        this.state = {
            cartItems: [],
            loading: true,
            error: null,
            step: 1, 
            formData: {
                firstName: '',
                lastName: '',
                email: '',
                phone: '',
                address: '',
                city: '',
                postalCode: '',
                deliveryMethod: 'courier',
                paymentMethod: 'card',
                comment: '',
                cardNumber: '',
                cardExpiry: '',
                cardCvv: ''
            },
            isSubmitting: false,
            orderSuccess: false,
            orderNumber: null,
            fieldErrors: {
                email: '',
                phone: '',
                cardNumber: '',
                cardExpiry: '',
                cardCvv: ''
            },
            notification: { show: false, message: '', type: 'success' }
        };
        this.abortController = null;
        this.cache = {
            userData: null,
            cartData: null,
            lastFetch: 0
        };
        this.notificationTimeout = null;
    }

    componentDidMount() {
        const passedCartItems = this.props.location?.state?.cartItems;
        
        if (passedCartItems && passedCartItems.length > 0) {
            console.log('Загрузка корзины из переданных данных, товаров:', passedCartItems.length);
            
            const formattedItems = passedCartItems.map(item => ({
                id: item.id,
                productId: item.productId,
                quantity: item.quantity,
                product: item.product ? {
                    id: item.product.id,
                    name: item.product.name,
                    price: item.product.price,
                    images: item.product.images || []
                } : null
            }));
            
            this.setState({ 
                cartItems: formattedItems,
                loading: false 
            });
        } 
        else if (this.context && this.context.state && this.context.state.cartItems && this.context.state.cartItems.length > 0) {
            console.log('Загрузка корзины из контекста, товаров:', this.context.state.cartItems.length);
            
            const formattedItems = this.context.state.cartItems.map(item => ({
                id: item.id,
                productId: item.productId,
                quantity: item.quantity,
                product: item.product ? {
                    id: item.product.id,
                    name: item.product.name,
                    price: item.product.price,
                    images: item.product.images || []
                } : null
            }));
            
            this.setState({ 
                cartItems: formattedItems,
                loading: false 
            });
        }
        else {
            console.log('Загрузка корзины с сервера');
            this.loadCart();
        }
        
        this.loadUserData();
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

    validateEmail = (email) => {
        const emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
        return emailRegex.test(email);
    };

    validatePhone = (phone) => {
        const phoneRegex = /^(\+7|7|8)?[\s\-]?\(?[0-9]{3}\)?[\s\-]?[0-9]{3}[\s\-]?[0-9]{2}[\s\-]?[0-9]{2}$/;
        const cleanPhone = phone.replace(/[\s\-\(\)]/g, '');
        const phoneRegexSimple = /^(\+7|7|8)?[0-9]{10}$/;
        return phoneRegex.test(phone) || phoneRegexSimple.test(cleanPhone);
    };

    formatPhone = (phone) => {
        const clean = phone.replace(/\D/g, '');
        if (clean.length === 11) {
            return `+${clean[0]} (${clean.slice(1, 4)}) ${clean.slice(4, 7)}-${clean.slice(7, 9)}-${clean.slice(9, 11)}`;
        }
        if (clean.length === 10) {
            return `+7 (${clean.slice(0, 3)}) ${clean.slice(3, 6)}-${clean.slice(6, 8)}-${clean.slice(8, 10)}`;
        }
        return phone;
    };

    validateName = (name) => {
        return name.trim().length >= 2 && /^[a-zA-Zа-яА-Я\s-]+$/.test(name);
    };

    validateField = (name, value) => {
        const errors = { ...this.state.fieldErrors };
        
        switch(name) {
            case 'email':
                if (!value) {
                    errors.email = 'Email обязателен';
                } else if (!this.validateEmail(value)) {
                    errors.email = 'Введите корректный email (example@mail.com)';
                } else {
                    errors.email = '';
                }
                break;
            case 'phone':
                if (!value) {
                    errors.phone = 'Телефон обязателен';
                } else if (!this.validatePhone(value)) {
                    errors.phone = 'Введите корректный номер телефона (например: +7 (999) 123-45-67)';
                } else {
                    errors.phone = '';
                }
                break;
            case 'firstName':
            case 'lastName':
                if (!value) {
                    errors[name] = 'Поле обязательно';
                } else if (!this.validateName(value)) {
                    errors[name] = 'Используйте только буквы (минимум 2 символа)';
                } else {
                    errors[name] = '';
                }
                break;
            default:
                break;
        }
        
        this.setState({ fieldErrors: errors });
        return !errors[name];
    };

    handleInputChange = (e) => {
        const { name, value } = e.target;
        let formattedValue = value;
        
        if (name === 'phone') {
            formattedValue = value;
        }
        
        this.setState(prevState => ({
            formData: {
                ...prevState.formData,
                [name]: formattedValue
            }
        }), () => {
            this.validateField(name, formattedValue);
        });
    };

    handlePhoneBlur = () => {
        const { phone } = this.state.formData;
        if (phone && this.validatePhone(phone)) {
            const formattedPhone = this.formatPhone(phone);
            this.setState(prevState => ({
                formData: {
                    ...prevState.formData,
                    phone: formattedPhone
                }
            }));
        }
    };

    handleCardNumberChange = (e) => {
        let value = e.target.value.replace(/\D/g, '');
        if (value.length > 16) value = value.slice(0, 16);
        let formattedValue = '';
        for (let i = 0; i < value.length; i++) {
            if (i > 0 && i % 4 === 0) {
                formattedValue += ' ';
            }
            formattedValue += value[i];
        }
        this.setState(prevState => ({
            formData: {
                ...prevState.formData,
                cardNumber: formattedValue
            },
            fieldErrors: {
                ...prevState.fieldErrors,
                cardNumber: value.length > 0 && value.length !== 16 ? 'Номер карты должен содержать 16 цифр' : ''
            }
        }));
    };

    handleCardExpiryChange = (e) => {
        let value = e.target.value.replace(/\D/g, '');
        if (value.length > 4) value = value.slice(0, 4);
        let formattedValue = '';
        for (let i = 0; i < value.length; i++) {
            if (i === 2) {
                formattedValue += '/';
            }
            formattedValue += value[i];
        }
        
        let error = '';
        if (value.length === 4) {
            const month = parseInt(value.slice(0, 2));
            const year = parseInt(value.slice(2, 4));
            const currentYear = new Date().getFullYear() % 100;
            const currentMonth = new Date().getMonth() + 1;
            
            if (month < 1 || month > 12) {
                error = 'Месяц должен быть от 01 до 12';
            } else if (year < currentYear || (year === currentYear && month < currentMonth)) {
                error = 'Срок действия карты истек';
            }
        } else if (value.length > 0) {
            error = 'Введите полный срок действия (ММ/ГГ)';
        }
        
        this.setState(prevState => ({
            formData: {
                ...prevState.formData,
                cardExpiry: formattedValue
            },
            fieldErrors: {
                ...prevState.fieldErrors,
                cardExpiry: error
            }
        }));
    };

    handleCardCvvChange = (e) => {
        let value = e.target.value.replace(/\D/g, '');
        if (value.length > 3) value = value.slice(0, 3);
        
        this.setState(prevState => ({
            formData: {
                ...prevState.formData,
                cardCvv: value
            },
            fieldErrors: {
                ...prevState.fieldErrors,
                cardCvv: value.length > 0 && value.length !== 3 ? 'CVV должен содержать 3 цифры' : ''
            }
        }));
    };

    validateCardData = () => {
        const { cardNumber, cardExpiry, cardCvv } = this.state.formData;
        const cleanCardNumber = cardNumber.replace(/\s/g, '');
        let isValid = true;
        
        if (cleanCardNumber.length !== 16) {
            this.setState(prevState => ({
                fieldErrors: {
                    ...prevState.fieldErrors,
                    cardNumber: 'Номер карты должен содержать 16 цифр'
                }
            }));
            isValid = false;
        }
        
        if (cardExpiry.length !== 5) {
            this.setState(prevState => ({
                fieldErrors: {
                    ...prevState.fieldErrors,
                    cardExpiry: 'Введите корректный срок действия (ММ/ГГ)'
                }
            }));
            isValid = false;
        }
        
        const expiryParts = cardExpiry.split('/');
        if (expiryParts.length === 2) {
            const month = parseInt(expiryParts[0]);
            const year = parseInt(expiryParts[1]);
            const currentYear = new Date().getFullYear() % 100;
            const currentMonth = new Date().getMonth() + 1;
            
            if (month < 1 || month > 12) {
                this.setState(prevState => ({
                    fieldErrors: {
                        ...prevState.fieldErrors,
                        cardExpiry: 'Введите корректный месяц (01-12)'
                    }
                }));
                isValid = false;
            } else if (year < currentYear || (year === currentYear && month < currentMonth)) {
                this.setState(prevState => ({
                    fieldErrors: {
                        ...prevState.fieldErrors,
                        cardExpiry: 'Срок действия карты истек'
                    }
                }));
                isValid = false;
            }
        }
        
        if (cardCvv.length !== 3) {
            this.setState(prevState => ({
                fieldErrors: {
                    ...prevState.fieldErrors,
                    cardCvv: 'CVV должен содержать 3 цифры'
                }
            }));
            isValid = false;
        }
        
        return isValid;
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

            const cacheKey = `cart_${userId}`;
            const cachedData = localStorage.getItem(cacheKey);
            const cacheTime = localStorage.getItem(`${cacheKey}_time`);
            const now = Date.now();
            
            if (cachedData && cacheTime && (now - parseInt(cacheTime)) < 300000) {
                const cartItems = JSON.parse(cachedData);
                const formattedItems = cartItems.map(item => ({
                    id: item.Id,
                    productId: item.ProductId,
                    quantity: item.Quantity,
                    product: item.Product ? {
                        id: item.Product.Id,
                        name: item.Product.Name,
                        price: item.Product.Price,
                        images: item.Product.Images || []
                    } : null
                }));
                this.setState({ cartItems: formattedItems, loading: false });
                return;
            }

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
            
            localStorage.setItem(cacheKey, JSON.stringify(cartItems));
            localStorage.setItem(`${cacheKey}_time`, now.toString());

            const formattedItems = cartItems.map(item => ({
                id: item.Id,
                productId: item.ProductId,
                quantity: item.Quantity,
                product: item.Product ? {
                    id: item.Product.Id,
                    name: item.Product.Name,
                    price: item.Product.Price,
                    images: item.Product.Images || []
                } : null
            }));
            
            this.setState({ 
                cartItems: formattedItems,
                loading: false
            });

        } catch (error) {
            if (error.name === 'AbortError') return;
            console.error('Ошибка загрузки корзины:', error);
            this.setState({ error: error.message, loading: false });
        }
    };

    loadUserData = async () => {
        try {
            const userId = localStorage.getItem('userId') || 1;
            const cacheKey = `user_${userId}`;
            const cachedData = localStorage.getItem(cacheKey);
            const cacheTime = localStorage.getItem(`${cacheKey}_time`);
            const now = Date.now();
            
            if (cachedData && cacheTime && (now - parseInt(cacheTime)) < 300000) {
                const userData = JSON.parse(cachedData);
                this.setState(prevState => ({
                    formData: {
                        ...prevState.formData,
                        firstName: userData.userName?.split(' ')[0] || '',
                        lastName: userData.userName?.split(' ')[1] || '',
                        email: userData.email || '',
                        phone: userData.phone || ''
                    }
                }));
                return;
            }
            
            const response = await fetch(`http://localhost:5214/api/Users/${userId}`, {
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('token')}`
                }
            });
            
            if (response.ok) {
                const userData = await response.json();
                
                localStorage.setItem(cacheKey, JSON.stringify(userData));
                localStorage.setItem(`${cacheKey}_time`, now.toString());
                
                this.setState(prevState => ({
                    formData: {
                        ...prevState.formData,
                        firstName: userData.userName?.split(' ')[0] || '',
                        lastName: userData.userName?.split(' ')[1] || '',
                        email: userData.email || '',
                        phone: userData.phone || ''
                    }
                }));
            }
        } catch (error) {
            console.error('Ошибка загрузки данных пользователя:', error);
        }
    };

    handleNextStep = () => {
        const { step, formData, fieldErrors } = this.state;

        if (step === 1) {
            if (!formData.firstName || !formData.lastName || !formData.email || !formData.phone) {
                this.showNotification('Пожалуйста, заполните все обязательные поля', 'error');
                return;
            }
            
            if (!this.validateName(formData.firstName)) {
                this.showNotification('Имя должно содержать только буквы (минимум 2 символа)', 'error');
                return;
            }
            
            if (!this.validateName(formData.lastName)) {
                this.showNotification('Фамилия должна содержать только буквы (минимум 2 символа)', 'error');
                return;
            }
            
            if (!this.validateEmail(formData.email)) {
                this.showNotification('Введите корректный email (например: user@example.com)', 'error');
                return;
            }
            
            if (!this.validatePhone(formData.phone)) {
                this.showNotification('Введите корректный номер телефона (например: +7 (999) 123-45-67)', 'error');
                return;
            }
        }
        
        if (step === 2) {
            if (!formData.address || !formData.city) {
                this.showNotification('Пожалуйста, заполните адрес доставки', 'error');
                return;
            }
        }
        
        if (step === 3 && formData.paymentMethod === 'card') {
            this.setState({ step: 4 });
            window.scrollTo(0, 0);
            return;
        }
        
        if (step === 3 && formData.paymentMethod === 'cash') {
            this.setState({ step: 5 });
            window.scrollTo(0, 0);
            return;
        }
        
        if (step === 4) {
            if (!this.validateCardData()) {
                const errors = Object.values(this.state.fieldErrors).filter(e => e);
                if (errors.length > 0) {
                    this.showNotification(errors[0], 'error');
                }
                return;
            }
            this.setState({ step: 5 });
            window.scrollTo(0, 0);
            return;
        }
        
        this.setState({ step: step + 1 });
        window.scrollTo(0, 0);
    };

    handlePrevStep = () => {
        const { step, formData } = this.state;
        
        if (step === 4) {
            this.setState({ step: 3 });
        } else if (step === 5) {
            if (formData.paymentMethod === 'card') {
                this.setState({ step: 4 });
            } else {
                this.setState({ step: 3 });
            }
        } else {
            this.setState({ step: step - 1 });
        }
        window.scrollTo(0, 0);
    };

    clearCache = () => {
        const keys = Object.keys(localStorage);
        keys.forEach(key => {
            if (key.startsWith('cart_') || key.startsWith('user_')) {
                localStorage.removeItem(key);
            }
        });
    };

    handleSubmitOrder = async () => {
        this.setState({ isSubmitting: true });
        
        try {
            const userId = localStorage.getItem('userId');
            const { cartItems, formData } = this.state;

            console.log('formData:', formData);

            if (!userId) {
                console.error('Нет userId в localStorage!');
                this.showNotification('Ошибка: пользователь не авторизован', 'error');
                this.setState({ isSubmitting: false });
                return;
            }

            if (cartItems.length === 0) {
                console.error('Корзина пуста!');
                this.showNotification('Корзина пуста. Добавьте товары для оформления заказа.', 'error');
                this.setState({ isSubmitting: false });
                return;
            }

            const orderData = {
                userId: parseInt(userId),
                items: cartItems.map(item => ({
                    productId: item.productId,
                    quantity: item.quantity,
                    price: item.product?.price
                })),
                firstName: formData.firstName,
                lastName: formData.lastName,
                email: formData.email,
                phone: formData.phone,
                address: formData.address,
                city: formData.city,
                postalCode: formData.postalCode,
                deliveryMethod: formData.deliveryMethod,
                paymentMethod: formData.paymentMethod,
                comment: formData.comment,
                totalAmount: this.getTotalPrice()
            };
            
            console.log('OrderData:', JSON.stringify(orderData, null, 2));
            
            const response = await fetch('http://localhost:5214/api/Orders', {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${localStorage.getItem('token')}`
                },
                body: JSON.stringify(orderData)
            });
            
            console.log('Статус ответа:', response.status);
            
            const result = await response.json();
            console.log('Ответ сервера:', result);
            
            if (response.ok) {
                let orderNumber = result.orderNumber || result.id || result.orderId || Math.floor(Math.random() * 1000000);
                
                localStorage.setItem('needRefreshProfile', 'true');
                
                this.setState({ 
                    orderSuccess: true, 
                    orderNumber: orderNumber,
                    step: 6,
                    isSubmitting: false
                });

                try {
                    await fetch(`http://localhost:5214/api/Baskets/clear/user/${parseInt(userId)}`, {
                        method: 'DELETE',
                        headers: {
                            'Authorization': `Bearer ${localStorage.getItem('token')}`
                        }
                    });
                    console.log('Корзина очищена');
                } catch (clearError) {
                    console.error('Ошибка очистки корзины:', clearError);
                }

                this.clearCache();
                
                if (this.context && this.context.updateCartCount) {
                    await this.context.updateCartCount();
                }
                
                this.showNotification(`Заказ успешно оформлен! Номер заказа: ${orderNumber}`, 'success');
            } else {
                console.error('Ошибка при создании заказа:', result);
                const errorMessage = result.message || result.title || 'Ошибка при создании заказа';
                this.showNotification(errorMessage, 'error');
                this.setState({ isSubmitting: false });
            }
        } catch (error) {
            console.error('Ошибка оформления заказа:', error);
            let errorMessage = 'Произошла ошибка при оформлении заказа. Пожалуйста, попробуйте снова.';
            this.showNotification(errorMessage, 'error');
            this.setState({ isSubmitting: false });
        }
    };

    getTotalPrice = () => {
        const { cartItems } = this.state;
        return cartItems.reduce((sum, item) => sum + (item.product?.price || 0) * item.quantity, 0);
    };

    getDeliveryPrice = () => {
        const { formData } = this.state;
        const total = this.getTotalPrice();
        
        if (formData.deliveryMethod === 'courier') {
            return total >= 3000 ? 0 : 300;
        } else if (formData.deliveryMethod === 'pickup') {
            return 0;
        }
        return 200;
    };

    getTotalWithDelivery = () => {
        return this.getTotalPrice() + this.getDeliveryPrice();
    };

    handleProfile = () => {
        this.props.navigate('/Profile');
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

    handleContinueShopping = () => {
        this.props.navigate('/HomePage');
    };

    renderStep1 = () => {
        const { formData, fieldErrors } = this.state;
        
        return (
            <div className="checkout-step">
                <h2 className="step-title">Контактная информация</h2>
                <div className="form-row">
                    <div className="form-group">
                        <label>Имя</label>
                        <input
                            type="text"
                            name="firstName"
                            value={formData.firstName}
                            onChange={this.handleInputChange}
                            className={`form-input ${fieldErrors.firstName ? 'error' : ''}`}
                            placeholder="Введите имя"
                        />
                        {fieldErrors.firstName && <span className="error-message">{fieldErrors.firstName}</span>}
                    </div>
                    <div className="form-group">
                        <label>Фамилия</label>
                        <input
                            type="text"
                            name="lastName"
                            value={formData.lastName}
                            onChange={this.handleInputChange}
                            className={`form-input ${fieldErrors.lastName ? 'error' : ''}`}
                            placeholder="Введите фамилию"
                        />
                        {fieldErrors.lastName && <span className="error-message">{fieldErrors.lastName}</span>}
                    </div>
                </div>
                <div className="form-row">
                    <div className="form-group">
                        <label>Email</label>
                        <input
                            type="email"
                            name="email"
                            value={formData.email}
                            onChange={this.handleInputChange}
                            className={`form-input ${fieldErrors.email ? 'error' : ''}`}
                            placeholder="example@mail.com"
                        />
                        {fieldErrors.email && <span className="error-message">{fieldErrors.email}</span>}
                    </div>
                    <div className="form-group">
                        <label>Телефон</label>
                        <input
                            type="tel"
                            name="phone"
                            value={formData.phone}
                            onChange={this.handleInputChange}
                            onBlur={this.handlePhoneBlur}
                            className={`form-input ${fieldErrors.phone ? 'error' : ''}`}
                            placeholder="+7 (999) 123-45-67"
                        />
                        {fieldErrors.phone && <span className="error-message">{fieldErrors.phone}</span>}
                    </div>
                </div>
            </div>
        );
    };

    renderStep2 = () => {
        const { formData } = this.state;
        
        return (
            <div className="checkout-step">
                <h2 className="step-title">Доставка</h2>
                <div className="delivery-methods">
                    <label className="delivery-option">
                        <input
                            type="radio"
                            name="deliveryMethod"
                            value="courier"
                            checked={formData.deliveryMethod === 'courier'}
                            onChange={this.handleInputChange}
                        />
                        <div className="delivery-info">
                            <strong>Курьерская доставка</strong>
                            <span>{this.getTotalPrice() >= 3000 ? 'Бесплатно' : '300 ₽'}</span>
                        </div>
                    </label>
                    <label className="delivery-option">
                        <input
                            type="radio"
                            name="deliveryMethod"
                            value="pickup"
                            checked={formData.deliveryMethod === 'pickup'}
                            onChange={this.handleInputChange}
                        />
                        <div className="delivery-info">
                            <strong>Самовывоз</strong>
                            <span>Бесплатно</span>
                        </div>
                    </label>
                </div>
                
                <div className="form-row">
                    <div className="form-group full-width">
                        <label>Адрес</label>
                        <input
                            type="text"
                            name="address"
                            value={formData.address}
                            onChange={this.handleInputChange}
                            className="form-input"
                            placeholder="Улица, дом, квартира"
                        />
                    </div>
                </div>
                <div className="form-row">
                    <div className="form-group">
                        <label>Город</label>
                        <input
                            type="text"
                            name="city"
                            value={formData.city}
                            onChange={this.handleInputChange}
                            className="form-input"
                            placeholder="Город"
                        />
                    </div>
                    <div className="form-group">
                        <label>Почтовый индекс</label>
                        <input
                            type="text"
                            name="postalCode"
                            value={formData.postalCode}
                            onChange={this.handleInputChange}
                            className="form-input"
                            placeholder="123456"
                        />
                    </div>
                </div>
            </div>
        );
    };

    renderStep3 = () => {
        const { formData } = this.state;
        
        return (
            <div className="checkout-step">
                <h2 className="step-title">Способ оплаты</h2>
                <div className="payment-methods">
                    <label className="payment-option">
                        <input
                            type="radio"
                            name="paymentMethod"
                            value="card"
                            checked={formData.paymentMethod === 'card'}
                            onChange={this.handleInputChange}
                        />
                        <div className="payment-info">
                            <strong>Банковская карта</strong>
                        </div>
                    </label>
                    <label className="payment-option">
                        <input
                            type="radio"
                            name="paymentMethod"
                            value="cash"
                            checked={formData.paymentMethod === 'cash'}
                            onChange={this.handleInputChange}
                        />
                        <div className="payment-info">
                            <strong>Наличные при получении</strong>
                        </div>
                    </label>
                </div>
                
                <div className="form-group full-width">
                    <label>Комментарий к заказу</label>
                    <textarea
                        name="comment"
                        value={formData.comment}
                        onChange={this.handleInputChange}
                        className="form-textarea"
                        rows="3"
                        placeholder="Дополнительная информация"
                    />
                </div>
            </div>
        );
    };

    renderCardStep = () => {
        const { formData, fieldErrors } = this.state;
        
        return (
            <div className="checkout-step">
                <h2 className="step-title">Данные банковской карты</h2>
                <div className="card-payment-form">
                    <div className="form-group full-width">
                        <label>Номер карты</label>
                        <input
                            type="text"
                            name="cardNumber"
                            value={formData.cardNumber}
                            onChange={this.handleCardNumberChange}
                            className={`form-input card-input ${fieldErrors.cardNumber ? 'error' : ''}`}
                            placeholder="1234 5678 9012 3456"
                            maxLength="19"
                        />
                        {fieldErrors.cardNumber && <span className="error-message">{fieldErrors.cardNumber}</span>}
                    </div>
                    
                    <div className="form-row">
                        <div className="form-group">
                            <label>Срок действия (ММ/ГГ)</label>
                            <input
                                type="text"
                                name="cardExpiry"
                                value={formData.cardExpiry}
                                onChange={this.handleCardExpiryChange}
                                className={`form-input ${fieldErrors.cardExpiry ? 'error' : ''}`}
                                placeholder="ММ/ГГ"
                                maxLength="5"
                            />
                            {fieldErrors.cardExpiry && <span className="error-message">{fieldErrors.cardExpiry}</span>}
                        </div>
                        <div className="form-group">
                            <label>CVV код</label>
                            <input
                                type="password"
                                name="cardCvv"
                                value={formData.cardCvv}
                                onChange={this.handleCardCvvChange}
                                className={`form-input cvv-input ${fieldErrors.cardCvv ? 'error' : ''}`}
                                placeholder="123"
                                maxLength="3"
                            />
                            {fieldErrors.cardCvv && <span className="error-message">{fieldErrors.cardCvv}</span>}
                        </div>
                    </div>
                </div>
            </div>
        );
    };

    renderStep5 = () => {
        const { orderSuccess, orderNumber } = this.state;
        
        if (orderSuccess) {
            return (
                <div className="checkout-success">
                    <div className="success-icon">✓</div>
                    <h2>Заказ успешно оформлен!</h2>
                    <p>Номер вашего заказа: <strong>{orderNumber}</strong></p>
                    <p>Подтверждение отправлено на вашу электронную почту.</p>
                    <button className="continue-shopping-btn" onClick={this.handleContinueShopping}>
                        Продолжить покупки
                    </button>
                </div>
            );
        }
        
        return (
            <div className="checkout-step">
                <h2 className="step-title">Подтверждение заказа</h2>
                <div className="order-summary">
                    <h3>Ваш заказ</h3>
                    {this.renderOrderItems()}
                    <div className="summary-totals">
                        <div className="summary-row">
                            <span>Товары ({this.state.cartItems.reduce((s, i) => s + i.quantity, 0)} шт.)</span>
                            <span>{this.getTotalPrice().toLocaleString('ru-RU')} ₽</span>
                        </div>
                        <div className="summary-row">
                            <span>Доставка</span>
                            <span>{this.getDeliveryPrice() === 0 ? 'Бесплатно' : `${this.getDeliveryPrice().toLocaleString('ru-RU')} ₽`}</span>
                        </div>
                        <div className="summary-total">
                            <span>Итого к оплате</span>
                            <span>{this.getTotalWithDelivery().toLocaleString('ru-RU')} ₽</span>
                        </div>
                    </div>
                </div>
            </div>
        );
    };

    renderOrderItems = () => {
        const { cartItems } = this.state;
        
        console.log('renderOrderItems - cartItems:', cartItems);
        
        return (
            <div className="order-items">
                {cartItems.map((item, index) => {
                    console.log(`Детальный анализ товара: ${item.product?.name}`);
                    console.log(`product.images:`, item.product?.images);
                    console.log(`product.images[0]:`, item.product?.images?.[0]);
                    console.log(`Все ключи в images[0]:`, item.product?.images?.[0] ? Object.keys(item.product.images[0]) : 'нет images');

                    let imageUrl = null;
                    if (item.product?.images && item.product.images.length > 0) {
                        const img = item.product.images[0];
                        imageUrl = img.imageUrl || img.ImageUrl || img.url || img.Url || null;
                        console.log(`Найденный URL:`, imageUrl);
                    }
                    
                    if (imageUrl && !imageUrl.startsWith('http')) {
                        imageUrl = `http://localhost:5214${imageUrl.startsWith('/') ? '' : '/'}${imageUrl}`;
                    }
                    
                    return (
                        <div key={item.id || index} className="order-item">
                            <div className="order-item-image">
                                {imageUrl ? (
                                    <img 
                                        src={imageUrl} 
                                        alt={item.product?.name || 'Товар'}
                                        onError={(e) => {
                                            console.error('Ошибка загрузки:', imageUrl);
                                            e.target.onerror = null;
                                            e.target.style.display = 'none';
                                        }}
                                        onLoad={() => console.log('Загружено:', imageUrl)}
                                    />
                                ) : (
                                    <div className="no-image">
                                        📷
                                        <small style={{fontSize: '10px', display: 'block'}}>
                                            {item.product?.images ? 'есть images но нет URL' : 'нет images'}
                                        </small>
                                    </div>
                                )}
                            </div>
                            <div className="order-item-info">
                                <div className="order-item-name">{item.product?.name || 'Товар'}</div>
                                <div className="order-item-quantity">Количество: {item.quantity}</div>
                            </div>
                            <div className="order-item-price">
                                {((item.product?.price || 0) * item.quantity).toLocaleString('ru-RU')} ₽
                            </div>
                        </div>
                    );
                })}
            </div>
        );
    };

    render() {
        const { loading, error, step, isSubmitting, cartItems, formData, notification } = this.state;

        if (loading) {
            return (
                <div className="checkout-page">
                    <div className="loading-spinner">
                        <div className="spinner"></div>
                        <p>Загрузка...</p>
                    </div>
                </div>
            );
        }

        if (error || cartItems.length === 0) {
            return (
                <div className="checkout-page">
                    <div className="empty-cart">
                        <h2>Корзина пуста</h2>
                        <p>Добавьте товары в корзину, чтобы оформить заказ</p>
                        <button className="continue-shopping-btn" onClick={this.handleContinueShopping}>
                            Перейти в каталог
                        </button>
                    </div>
                </div>
            );
        }

        const isCardStep = step === 4 && formData.paymentMethod === 'card';
        const isConfirmationStep = (step === 5) || (step === 4 && formData.paymentMethod === 'cash');
        const isSuccessStep = step === 6;
        const showBackButton = step > 1 && !isSuccessStep;

        return (
            <div className="checkout-page">
                {notification.show && (
                    <div className={`checkout-notification ${notification.type === 'error' ? 'checkout-notification-error' : ''}`}>
                        <div className="checkout-notification-content">
                            <div className="checkout-notification-text">
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
                    <div className="container checkout-container">
                        <div className="checkout-header">
                            <h1 className="checkout-title">Оформление заказа</h1>
                            <button className="back-button" onClick={this.handleGoBack}>
                                Назад 
                            </button>
                            <div className="checkout-steps">
                                <div className={`step-indicator ${step >= 1 ? 'active' : ''}`}>
                                    <span className="step-number">1</span>
                                    <span className="step-label">Информация</span>
                                </div>
                                <div className="step-line"></div>
                                <div className={`step-indicator ${step >= 2 ? 'active' : ''}`}>
                                    <span className="step-number">2</span>
                                    <span className="step-label">Доставка</span>
                                </div>
                                <div className="step-line"></div>
                                <div className={`step-indicator ${step >= 3 ? 'active' : ''}`}>
                                    <span className="step-number">3</span>
                                    <span className="step-label">Оплата</span>
                                </div>
                                <div className="step-line"></div>
                                {formData.paymentMethod === 'card' && (
                                    <>
                                        <div className={`step-indicator ${step >= 4 ? 'active' : ''}`}>
                                            <span className="step-number">4</span>
                                            <span className="step-label">Карта</span>
                                        </div>
                                        <div className="step-line"></div>
                                    </>
                                )}
                                <div className={`step-indicator ${isConfirmationStep || isSuccessStep ? 'active' : ''}`}>
                                    <span className="step-number">{formData.paymentMethod === 'card' ? '5' : '4'}</span>
                                    <span className="step-label">Подтверждение</span>
                                </div>
                            </div>
                        </div>

                        <div className="checkout-content">
                            <div className="checkout-form">
                                {step === 1 && this.renderStep1()}
                                {step === 2 && this.renderStep2()}
                                {step === 3 && this.renderStep3()}
                                {isCardStep && this.renderCardStep()}
                                {isConfirmationStep && this.renderStep5()}
                            </div>

                            <div className="checkout-sidebar">
                                <div className="order-summary-card">
                                    <h3>Ваш заказ</h3>
                                    <div className="order-items-summary">
                                        {cartItems.slice(0, 3).map((item, index) => {
                                            // Получаем URL изображения для миниатюры в боковой панели
                                            let imageUrl = item.product?.images?.[0]?.imageUrl;
                                            if (imageUrl && !imageUrl.startsWith('http')) {
                                                imageUrl = `http://localhost:5214${imageUrl.startsWith('/') ? '' : '/'}${imageUrl}`;
                                            }
                                            
                                            return (
                                                <div key={item.id || index} className="summary-item">
                                                    <div className="summary-item-info">
                                                        {imageUrl && (
                                                            <img 
                                                                src={imageUrl} 
                                                                alt={item.product?.name || 'Товар'}
                                                                className="summary-item-image"
                                                                onError={(e) => {
                                                                    e.target.onerror = null;
                                                                    e.target.style.display = 'none';
                                                                }}
                                                            />
                                                        )}
                                                        <div className="summary-item-details">
                                                            <div className="summary-item-name">{item.product?.name || 'Товар'}</div>
                                                            <div className="summary-item-quantity">x{item.quantity}</div>
                                                        </div>
                                                    </div>
                                                    <div className="summary-item-price">
                                                        {((item.product?.price || 0) * item.quantity).toLocaleString('ru-RU')} ₽
                                                    </div>
                                                </div>
                                            );
                                        })}
                                        {cartItems.length > 3 && (
                                            <div className="summary-more">
                                                и еще {cartItems.length - 3} товар(ов)
                                            </div>
                                        )}
                                    </div>
                                    <div className="summary-divider"></div>
                                    <div className="summary-row">
                                        <span>Товары</span>
                                        <span>{this.getTotalPrice().toLocaleString('ru-RU')} ₽</span>
                                    </div>
                                    <div className="summary-row">
                                        <span>Доставка</span>
                                        <span>{this.getDeliveryPrice() === 0 ? 'Бесплатно' : `${this.getDeliveryPrice().toLocaleString('ru-RU')} ₽`}</span>
                                    </div>
                                    <div className="summary-total">
                                        <span>Итого</span>
                                        <span>{this.getTotalWithDelivery().toLocaleString('ru-RU')} ₽</span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div className="checkout-actions">
                            {showBackButton && step < 5 && (
                                <button 
                                    className="btn-prev" 
                                    onClick={this.handlePrevStep}
                                >
                                    Назад
                                </button>
                            )}
                            {step < 5 && !isSuccessStep && (
                                <button className="btn-next" onClick={this.handleNextStep}>
                                    Далее 
                                </button>
                            )}
                            {isConfirmationStep && (
                                <button 
                                    className="btn-submit" 
                                    onClick={this.handleSubmitOrder}
                                    disabled={isSubmitting}
                                >
                                    {isSubmitting ? 'Оформление...' : 'Подтвердить заказ'}
                                </button>
                            )}
                        </div>
                    </div>
                </main>
            </div>
        );
    }
}

export default withNavigate(CheckoutPage);