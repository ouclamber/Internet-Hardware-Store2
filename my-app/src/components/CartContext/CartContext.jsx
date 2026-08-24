import React, { Component, createContext } from 'react';
import axios from 'axios';

export const CartContext = createContext();

class CartProvider extends Component {
    constructor(props) {
        super(props);
        this.state = {
            cartItems: [],
            cartCount: 0,
            loading: false
        };
    }

    componentDidMount() {
        this.loadCart();
    }

    getUserId = () => {
        const userId = localStorage.getItem('userId');
        if (!userId) {
            console.error('Пользователь не авторизован');
            return null;
        }
        return parseInt(userId);
    };

    loadCart = async () => {
        const userId = this.getUserId();
        if (!userId) return;

        try {
            this.setState({ loading: true });
            
            const token = localStorage.getItem('token');
            const response = await axios.get(`http://localhost:5214/api/Baskets/user/${userId}`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            
            const cartItems = response.data || [];
            const cartCount = cartItems.reduce((sum, item) => sum + (item.Quantity || 0), 0);
            
            this.setState({ cartItems, cartCount, loading: false });
        } catch (error) {
            console.error('Ошибка загрузки корзины:', error);
            this.setState({ loading: false });
        }
    };

    addToCart = async (productId, quantity) => {
        const userId = this.getUserId();
        if (!userId) {
            alert('Пожалуйста, войдите в систему');
            return { success: false };
        }

        try {
            const token = localStorage.getItem('token');
            const response = await axios.post('http://localhost:5214/api/Baskets', 
                { userId, productId, quantity },
                {
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                }
            );
            
            if (response.data.success) {
                await this.loadCart();
                return { success: true };
            }
            return { success: false };
        } catch (error) {
            console.error('Ошибка добавления в корзину:', error);
            return { success: false };
        }
    };

    updateCartItem = async (itemId, quantity) => {
        const token = localStorage.getItem('token');
        try {
            await axios.put(`http://localhost:5214/api/Baskets/${itemId}`, 
                { quantity },
                {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                }
            );
            await this.loadCart();
        } catch (error) {
            console.error('Ошибка обновления корзины:', error);
        }
    };

    removeFromCart = async (itemId) => {
        const token = localStorage.getItem('token');
        try {
            await axios.delete(`http://localhost:5214/api/Baskets/${itemId}`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            await this.loadCart();
        } catch (error) {
            console.error('Ошибка удаления из корзины:', error);
        }
    };

    clearCart = async () => {
        const userId = this.getUserId();
        if (!userId) return { success: false };

        const token = localStorage.getItem('token');
        try {
            await axios.delete(`http://localhost:5214/api/Baskets/clear/user/${userId}`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            await this.loadCart();
            return { success: true };
        } catch (error) {
            console.error('Ошибка очистки корзины:', error);
            return { success: false };
        }
    };

    updateCartCount = () => {
        this.loadCart();
    };

    render() {
        return (
            <CartContext.Provider value={{
                ...this.state,
                addToCart: this.addToCart,
                updateCartItem: this.updateCartItem,
                removeFromCart: this.removeFromCart,
                clearCart: this.clearCart,
                updateCartCount: this.updateCartCount,
                refreshCart: this.loadCart
            }}>
                {this.props.children}
            </CartContext.Provider>
        );
    }
}

export default CartProvider;