import React, { Component } from 'react';
import './SignUp.css';
import {
    TextField,
    Radio,
    RadioGroup,
    FormControlLabel,
    Button,
    Alert,
    CircularProgress,
    Snackbar
} from '@mui/material';
import { useNavigate } from 'react-router-dom'; 
import AuthServices from '../services/AuthServices';

const authService = new AuthServices();

function withNavigate(Component) {
    return function WrappedComponent(props) {
        const navigate = useNavigate();
        return <Component {...props} navigate={navigate} />;
    }
}

class SignUp extends Component {
    constructor(props) {
        super(props);
        this.state = {
            UserName: '',
            Password: '',
            ConfirmPassword: '',
            RoleValue: 'User',
            UserNameFlag: false,
            PasswordFlag: false,
            ConfirmPasswordFlag: false,
            UserNameError: '',
            PasswordError: '',
            ConfirmPasswordError: '',
            GeneralError: '',
            Loading: false,
            SnackbarOpen: false,
            SnackbarMessage: '',
            SnackbarSeverity: 'success'
        };
    }

    componentDidMount() {
        this.setState({
            UserName: '',
            Password: '',
            ConfirmPassword: '',
            RoleValue: 'User',
            UserNameFlag: false,
            PasswordFlag: false,
            ConfirmPasswordFlag: false,
            UserNameError: '',
            PasswordError: '',
            ConfirmPasswordError: '',
            GeneralError: '',
            Loading: false,
            SnackbarOpen: false
        });

        localStorage.removeItem('token');
        localStorage.removeItem('userId');
        localStorage.removeItem('userName');
        localStorage.removeItem('userRole');
        localStorage.removeItem('needRefreshProfile');

        const inputs = document.querySelectorAll('input');
        inputs.forEach(input => {
            if (input) input.value = '';
        });
    }

    handleCloseSnackbar = () => {
        this.setState({ SnackbarOpen: false });
    }

    handleValues = (e) => {
        const { name, value } = e.target;
        this.setState({ 
            [name]: value,
            GeneralError: '',
            [name + 'Flag']: false,
            [name + 'Error']: ''
        });
    }

    handleChangeRole = (e) => {
        this.setState({ RoleValue: e.target.value });
    }

    validateInputs = () => {
        let isValid = true;
        
        if (!this.state.UserName.trim()) {
            this.setState({ UserNameFlag: true, UserNameError: 'Введите имя пользователя' });
            isValid = false;
        }
        if (!this.state.Password) {
            this.setState({ PasswordFlag: true, PasswordError: 'Введите пароль' });
            isValid = false;
        } else if (this.state.Password.length < 6) {
            this.setState({ PasswordFlag: true, PasswordError: 'Пароль должен быть минимум 6 символов' });
            isValid = false;
        }
        if (!this.state.ConfirmPassword) {
            this.setState({ ConfirmPasswordFlag: true, ConfirmPasswordError: 'Подтвердите пароль' });
            isValid = false;
        } else if (this.state.Password !== this.state.ConfirmPassword) {
            this.setState({ ConfirmPasswordFlag: true, ConfirmPasswordError: 'Пароли не совпадают' });
            isValid = false;
        }
        
        return isValid;
    }

    handleSubmit = async () => {
        if (!this.validateInputs()) return;

        this.setState({ Loading: true, GeneralError: '' });

        let data = {
            UserName: this.state.UserName.trim(),
            Password: this.state.Password,
            ConfirmPassword: this.state.ConfirmPassword,
            Role: this.state.RoleValue,
        };

        console.log('Отправляемые данные для регистрации:', data);

        try {
            const response = await authService.SignUp(data);
            console.log('SignUp response:', response);
            console.log('Статус ответа:', response?.status);
            
            if (response?.status === 200 || response?.status === 201) {
                localStorage.clear();

                this.setState({
                    UserName: '',
                    Password: '',
                    ConfirmPassword: '',
                    RoleValue: 'User',
                    Loading: false,
                    SnackbarOpen: true,
                    SnackbarMessage: `Аккаунт "${this.state.UserName.trim()}" успешно создан! Перенаправление на страницу входа...`,
                    SnackbarSeverity: 'success'
                });

                setTimeout(() => {
                    this.props.navigate('/SignIn');
                }, 2500);
                
            } else {
                const errorMsg = response?.data?.message || 'Ошибка регистрации';
                this.setState({ GeneralError: errorMsg, Loading: false });
            }
        } catch (error) {
            console.error('SignUp error:', error);
            let errorMsg = 'Ошибка при регистрации';
            
            if (error.response?.data) {
                const data = error.response.data;
                if (error.response.status === 409) {
                    errorMsg = 'Пользователь с таким именем уже существует. Пожалуйста, выберите другое имя.';
                    this.setState({ UserNameFlag: true, UserNameError: 'Имя уже занято' });
                } else if (data.errors) {
                    const errors = Object.values(data.errors).flat();
                    errorMsg = errors[0] || data.message || 'Ошибка валидации';
                } else {
                    errorMsg = data.message || data.title || 'Ошибка регистрации';
                }
            } else if (error.request) {
                errorMsg = 'Нет связи с сервером. Проверьте подключение.';
            }
            
            this.setState({ GeneralError: errorMsg, Loading: false });
        }
    }

    handleSignIn = () => {
        this.props.navigate('/SignIn');
    }

    render() {
        const { 
            Loading, 
            GeneralError, 
            UserNameError, 
            PasswordError, 
            ConfirmPasswordError, 
            UserNameFlag, 
            PasswordFlag, 
            ConfirmPasswordFlag,
            SnackbarOpen,
            SnackbarMessage,
            SnackbarSeverity
        } = this.state;
        
        return (
            <div className='SignUp-Container'>
                <Snackbar
                    open={SnackbarOpen}
                    autoHideDuration={3000}
                    onClose={this.handleCloseSnackbar}
                    anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
                >
                    <Alert 
                        onClose={this.handleCloseSnackbar} 
                        severity={SnackbarSeverity}
                        sx={{ 
                            width: '100%',
                            fontSize: '16px',
                            '& .MuiAlert-message': {
                                display: 'flex',
                                alignItems: 'center'
                            }
                        }}
                    >
                        {SnackbarMessage}
                    </Alert>
                </Snackbar>

                <div className='SignUp-SubContainer'>
                    <div className='Header'>SignUp</div>
                    <div className='Body'>
                        <form className='form'>
                            {GeneralError && (
                                <Alert severity="error" sx={{ mb: 2, width: '300px' }}>
                                    {GeneralError}
                                </Alert>
                            )}
                            <TextField
                                error={UserNameFlag}
                                className='TextField'
                                name='UserName'
                                label="UserName"
                                variant="outlined"
                                size='small'
                                value={this.state.UserName}
                                onChange={this.handleValues}
                                helperText={UserNameError}
                                disabled={Loading}
                            />
                            <TextField
                                error={PasswordFlag}
                                className='TextField'
                                name='Password'
                                label="Password"
                                variant="outlined"
                                size='small'
                                type="password"
                                value={this.state.Password}
                                onChange={this.handleValues}
                                helperText={PasswordError}
                                disabled={Loading}
                            />
                            <TextField
                                error={ConfirmPasswordFlag}
                                className='TextField'
                                name='ConfirmPassword'
                                label="Confirm Password"
                                variant="outlined"
                                size='small'
                                type="password"
                                value={this.state.ConfirmPassword}
                                onChange={this.handleValues}
                                helperText={ConfirmPasswordError}
                                disabled={Loading}
                            />
                            <RadioGroup
                                className='Roles'
                                name="Role"
                                value={this.state.RoleValue}
                                onChange={this.handleChangeRole}
                            >
                                <FormControlLabel
                                    className='RoleValue'
                                    value="Admin"
                                    control={<Radio />}
                                    label="Admin"
                                />
                                <FormControlLabel
                                    className='RoleValue'
                                    value="User"
                                    control={<Radio />}
                                    label="User"
                                />
                            </RadioGroup>
                        </form>
                    </div>
                    <div className='buttons'>
                        <Button 
                            className='Btn' 
                            color='primary'
                            onClick={this.handleSignIn}
                            disabled={Loading}
                        >
                            Sign In
                        </Button>
                        <Button 
                            className='Btn' 
                            variant="contained" 
                            color='primary' 
                            onClick={this.handleSubmit}
                            disabled={Loading}
                        >
                            {Loading ? <CircularProgress size={24} /> : 'Sign Up'}
                        </Button>
                    </div>
                </div>
            </div>
        );
    }
}

export default withNavigate(SignUp);