document.addEventListener('DOMContentLoaded', function() {
    // GSAP이 로드되었는지 확인
    if (typeof gsap !== 'undefined') {
        // Hero section animations
        const tl = gsap.timeline();
        tl.from('.logo, .logo-container h2', { opacity: 0, y: -50, duration: 1, ease: 'power3.out' })
          .from('.main-title', { opacity: 0, y: 50, duration: 1, ease: 'power3.out' }, '-=0.5')
          .from('.tagline', { opacity: 0, y: 30, duration: 1, ease: 'power3.out' }, '-=0.7')
          .from('.cta-buttons', { opacity: 0, y: 30, duration: 1, ease: 'power3.out' }, '-=0.7');
        
        // ScrollTrigger 플러그인이 있는 경우에만 실행
        if (gsap.registerPlugin && gsap.ScrollTrigger) {
            gsap.registerPlugin(ScrollTrigger);
            
            // Feature cards animation
            gsap.from('.feature-card', {
                scrollTrigger: {
                    trigger: '.features-section',
                    start: 'top 80%'
                },
                y: 100,
                opacity: 0,
                duration: 1,
                stagger: 0.2,
                ease: 'power3.out'
            });
            
            // Testimonial animation
            gsap.from('.testimonial-card', {
                scrollTrigger: {
                    trigger: '.testimonials-section',
                    start: 'top 80%'
                },
                x: -100,
                opacity: 0,
                duration: 1,
                ease: 'power3.out'
            });
            
            // Stats animation
            gsap.from('.stat-item', {
                scrollTrigger: {
                    trigger: '.stats-section',
                    start: 'top 80%'
                },
                y: 50,
                opacity: 0,
                duration: 1,
                stagger: 0.2,
                ease: 'power3.out'
            });
        }
    } else {
        // GSAP이 없는 경우 기본 애니메이션으로 폴백
        document.querySelectorAll('.animated-element').forEach(el => {
            el.style.opacity = 1;
            el.style.transform = 'none';
        });
    }
    
    // 기본 스크롤 이벤트 처리
    window.addEventListener('scroll', function() {
        const navbar = document.querySelector('.navbar');
        if (navbar) {
            if (window.scrollY > 100) {
                navbar.classList.add('navbar-scrolled');
            } else {
                navbar.classList.remove('navbar-scrolled');
            }
        }
    });
    
    // Parallax effect for hero section
    window.addEventListener('mousemove', function(e) {
        const hero = document.querySelector('.hero-section');
        if (hero) {
            const x = e.clientX / window.innerWidth;
            const y = e.clientY / window.innerHeight;
            
            gsap.to('.hero-content', {
                x: (x - 0.5) * 20,
                y: (y - 0.5) * 20,
                duration: 1,
                ease: 'power1.out'
            });
        }
    });
});
